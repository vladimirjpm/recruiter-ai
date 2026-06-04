using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Domain.Enums;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Options;

namespace RecruiterAi.Infrastructure.Services;

public sealed class OpenAiCvGenerationService : ICvGenerationService
{
    private const string PromptVersion = "v1";
    private const int MaxRetries = 3;

    private readonly ChatClient _chatClient;
    private readonly string _model;
    private readonly ILogger<OpenAiCvGenerationService> _logger;

    // Default mix for count=10 (indices into FitLevels, round-robin for other counts)
    private static readonly string[] DefaultMixOrder =
    [
        "excellent_fit", "strong_fit", "medium_fit",
        "excellent_fit", "strong_fit", "medium_fit",
        "weak_fit", "overqualified", "missing_key_requirement", "career_switcher",
    ];

    private static readonly IReadOnlyDictionary<string, ScoreRange> ScoreRanges =
        new Dictionary<string, ScoreRange>
        {
            ["excellent_fit"]           = new(85, 100),
            ["strong_fit"]              = new(70, 84),
            ["medium_fit"]              = new(40, 69),
            ["weak_fit"]                = new(20, 39),
            ["overqualified"]           = new(50, 75),
            ["underqualified"]          = new(10, 30),
            ["missing_key_requirement"] = new(15, 35),
            ["career_switcher"]         = new(30, 55),
            ["related_industry"]        = new(35, 60),
            ["risk_profile"]            = new(20, 50),
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public OpenAiCvGenerationService(
        IOptions<LlmOptions> options,
        ILogger<OpenAiCvGenerationService> logger)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.ApiKey))
            throw new InvalidOperationException(
                "Llm:ApiKey is not configured. " +
                "Set it via appsettings.json or the LLM__APIKEY environment variable.");

        _model     = opt.Model;
        _chatClient = new ChatClient(_model, opt.ApiKey);
        _logger    = logger;
    }

    public async Task<CvGenerationResult> GenerateAsync(
        Position position,
        int count,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();

        _logger.LogInformation(new EventId(4001, "GenerationBatchStarted"),
            "CV generation started. BatchId={BatchId} PositionId={PositionId} Count={Count} Model={Model}",
            batchId, position.Id, count, _model);

        var sw = Stopwatch.StartNew();

        // Step 1: infer domain requirements from the job description
        List<string> inferredRequirements;
        try
        {
            inferredRequirements = await InferRequirementsAsync(position, batchId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(4003, "GenerationBatchFailed"), ex,
                "Requirements inference failed. BatchId={BatchId}", batchId);
            throw;
        }

        _logger.LogInformation(new EventId(4004, "RequirementsInferred"),
            "Requirements inferred. BatchId={BatchId} Count={Count}",
            batchId, inferredRequirements.Count);

        // Build the fit-level distribution for the requested count
        var mix = BuildMix(count);

        // Step 2: generate all N synthetic CVs in one LLM call
        List<LlmCandidateResponse> generated;
        try
        {
            generated = await GenerateCandidatesAsync(position, inferredRequirements, mix, batchId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(4003, "GenerationBatchFailed"), ex,
                "Candidate generation failed. BatchId={BatchId}", batchId);
            throw;
        }

        sw.Stop();

        _logger.LogInformation(new EventId(4005, "CandidatesGenerated"),
            "Candidates generated. BatchId={BatchId} Generated={Generated} DurationMs={DurationMs}",
            batchId, generated.Count, sw.ElapsedMilliseconds);

        // Map LLM output → domain entities
        var batch = new CvGenerationBatch
        {
            Id                   = batchId,
            PositionId           = position.Id,
            RequestedCount       = count,
            InferredRequirements = inferredRequirements,
            CandidateTypes       = mix,
            CreatedAt            = DateTimeOffset.UtcNow,
        };

        var candidates = generated.Select(g =>
        {
            var fitLevel = g.FitLevel;
            var range    = ScoreRanges.GetValueOrDefault(fitLevel, new ScoreRange(20, 80));
            return new Candidate
            {
                Id                  = Guid.NewGuid(),
                Name                = g.Name,
                Email               = g.Email,
                FileName            = $"{g.Name.Replace(' ', '_').ToLowerInvariant()}_generated.txt",
                StoragePath         = null,
                RawText             = g.CvText,
                Language            = "en",
                Source              = CandidateSource.Generated,
                ExpectedFitLevel    = fitLevel,
                ExpectedScoreRange  = range,
                UploadedAt          = DateTimeOffset.UtcNow,
                CvGenerationBatchId = batchId,
            };
        }).ToList();

        _logger.LogInformation(new EventId(4002, "GenerationBatchCompleted"),
            "Generation batch completed. BatchId={BatchId} PositionId={PositionId} Candidates={Candidates}",
            batchId, position.Id, candidates.Count);

        return new CvGenerationResult(batch, candidates);
    }

    // Step 1: call LLM to extract domain-specific requirements from job description
    private async Task<List<string>> InferRequirementsAsync(
        Position position,
        Guid batchId,
        CancellationToken ct)
    {
        var systemPrompt = """
            You are an expert HR analyst. Extract concise, domain-specific requirements
            from the job description. Output a flat list of requirement strings — skills,
            certifications, experience levels, deal-breakers, etc.
            Be domain-agnostic: derive requirements from the actual text, not assumptions.
            """;

        var userPrompt = $"""
            Extract requirements from the following job posting.

            Title: {position.Title}
            Description: {position.Description}
            Required Skills: {string.Join(", ", position.RequiredSkills)}
            Nice to Have: {(position.NiceToHaveSkills.Count > 0 ? string.Join(", ", position.NiceToHaveSkills) : "None")}
            Seniority: {position.SeniorityLevel ?? "Not specified"}
            Country: {position.Country ?? "Not specified"}
            """;

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(userPrompt),
        };

        var options = new ChatCompletionOptions
        {
            Temperature    = 0f,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "inferred_requirements",
                BinaryData.FromString(InferredRequirementsSchema),
                jsonSchemaIsStrict: true),
        };

        var completion = await CallWithRetryAsync(messages, options, batchId, ct);

        var parsed = JsonSerializer.Deserialize<LlmInferredRequirements>(
            completion.Content[0].Text, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI returned null for inferred requirements.");

        return parsed.Requirements;
    }

    // Step 2: generate all N CVs in a single LLM call using the specified fit-level mix
    private async Task<List<LlmCandidateResponse>> GenerateCandidatesAsync(
        Position position,
        List<string> inferredRequirements,
        Dictionary<string, int> mix,
        Guid batchId,
        CancellationToken ct)
    {
        var mixDescription = BuildMixDescription(mix);

        var systemPrompt = """
            You are an expert HR data generator. Create realistic synthetic candidate profiles
            for testing a resume screening system.

            Rules:
            - All names, emails, phone numbers, and company names must be completely fictional.
            - Tailor CVs to the specified domain — derive all skills and terminology from the
              job requirements, not from generic IT assumptions.
            - Each CV must clearly match its assigned fit_level relative to the position.
            - cv_text should be a realistic resume with work history, skills, and education.
            - Write concise but realistic CVs — 150 to 400 words each.
            """;

        var userPrompt = $"""
            Generate synthetic candidate CVs for the following position.

            POSITION:
            Title: {position.Title}
            Description: {position.Description}
            Required Skills: {string.Join(", ", position.RequiredSkills)}
            Nice to Have: {(position.NiceToHaveSkills.Count > 0 ? string.Join(", ", position.NiceToHaveSkills) : "None")}
            Seniority: {position.SeniorityLevel ?? "Not specified"}

            INFERRED REQUIREMENTS:
            {string.Join("\n", inferredRequirements.Select(r => $"- {r}"))}

            REQUIRED DISTRIBUTION ({mix.Values.Sum()} total candidates):
            {mixDescription}

            For each candidate, assign fit_level exactly as specified above.
            The expected_score_min and expected_score_max should reflect how well
            this candidate would score against the position (0–100 scale).
            """;

        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(userPrompt),
        };

        var options = new ChatCompletionOptions
        {
            Temperature    = 0.7f,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "generated_candidates",
                BinaryData.FromString(GeneratedCandidatesSchema),
                jsonSchemaIsStrict: true),
        };

        var completion = await CallWithRetryAsync(messages, options, batchId, ct);

        var parsed = JsonSerializer.Deserialize<LlmGeneratedCandidates>(
            completion.Content[0].Text, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI returned null for generated candidates.");

        return parsed.Candidates;
    }

    private async Task<ChatCompletion> CallWithRetryAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions options,
        Guid batchId,
        CancellationToken ct)
    {
        Exception? lastEx = null;
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await _chatClient.CompleteChatAsync(messages, options, ct);
            }
            catch (Exception ex)
            {
                lastEx = ex;
                if (attempt < MaxRetries)
                {
                    _logger.LogWarning(ex,
                        "OpenAI attempt {Attempt}/{Max} failed. BatchId={BatchId}",
                        attempt, MaxRetries, batchId);
                    await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
                }
            }
        }

        throw new InvalidOperationException(
            $"OpenAI call failed after {MaxRetries} attempts.", lastEx);
    }

    // Distributes N slots across fit levels using the default mix template, round-robin for remainder.
    private static Dictionary<string, int> BuildMix(int count)
    {
        var mix = new Dictionary<string, int>();
        for (int i = 0; i < count; i++)
        {
            var level = DefaultMixOrder[i % DefaultMixOrder.Length];
            mix[level] = mix.GetValueOrDefault(level) + 1;
        }
        return mix;
    }

    private static string BuildMixDescription(Dictionary<string, int> mix)
    {
        var sb = new StringBuilder();
        foreach (var (level, n) in mix)
            sb.AppendLine($"- {level}: {n} candidate(s)");
        return sb.ToString().TrimEnd();
    }

    // ── JSON Schemas (strict mode) ────────────────────────────────────────────

    private const string InferredRequirementsSchema = """
        {
          "type": "object",
          "properties": {
            "requirements": {
              "type": "array",
              "items": { "type": "string" }
            }
          },
          "required": ["requirements"],
          "additionalProperties": false
        }
        """;

    private const string GeneratedCandidatesSchema = """
        {
          "type": "object",
          "properties": {
            "candidates": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name":               { "type": "string" },
                  "email":              { "type": "string" },
                  "fit_level":          { "type": "string", "enum": ["excellent_fit","strong_fit","medium_fit","weak_fit","overqualified","underqualified","missing_key_requirement","career_switcher","related_industry","risk_profile"] },
                  "expected_score_min": { "type": "integer" },
                  "expected_score_max": { "type": "integer" },
                  "cv_text":            { "type": "string" }
                },
                "required": ["name","email","fit_level","expected_score_min","expected_score_max","cv_text"],
                "additionalProperties": false
              }
            }
          },
          "required": ["candidates"],
          "additionalProperties": false
        }
        """;

    // ── Internal response DTOs ────────────────────────────────────────────────

    private sealed record LlmInferredRequirements(List<string> Requirements);

    private sealed record LlmGeneratedCandidates(List<LlmCandidateResponse> Candidates);

    private sealed record LlmCandidateResponse(
        string Name,
        string Email,
        [property: JsonPropertyName("fit_level")]          string FitLevel,
        [property: JsonPropertyName("expected_score_min")] int ExpectedScoreMin,
        [property: JsonPropertyName("expected_score_max")] int ExpectedScoreMax,
        [property: JsonPropertyName("cv_text")]            string CvText);
}
