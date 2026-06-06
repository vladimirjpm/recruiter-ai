using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Options;

namespace RecruiterAi.Infrastructure.Services;

public sealed class OpenAiJobDescriptionExtractorService : IJobDescriptionExtractorService
{
    private const string PromptVersion = "v1";
    // 20k chars ≈ ~5k tokens — enough for any real JD; blocks SAP-architect / government-tender edge cases.
    private const int MaxInputChars = 20_000;
    private const int MinInputChars = 100;

    private readonly ChatClient _chatClient;
    private readonly string _model;
    private readonly ILogger<OpenAiJobDescriptionExtractorService> _logger;

    public OpenAiJobDescriptionExtractorService(
        IOptions<LlmOptions> options,
        ILogger<OpenAiJobDescriptionExtractorService> logger)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.ApiKey))
            throw new InvalidOperationException(
                "Llm:ApiKey is not configured.");

        _model = opt.Model;
        _chatClient = new ChatClient(_model, opt.ApiKey);
        _logger = logger;
    }

    public async Task<JobDescriptionExtractionResult> ExtractAsync(
        string jobDescriptionText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobDescriptionText) || jobDescriptionText.Length < MinInputChars)
            throw new ArgumentException(
                $"Job description must be at least {MinInputChars} characters.", nameof(jobDescriptionText));

        var inputText = jobDescriptionText.Length > MaxInputChars
            ? jobDescriptionText[..MaxInputChars]
            : jobDescriptionText;

        _logger.LogInformation(
            new EventId(4001, "JdExtractionStarted"),
            "JD extraction started. Model={Model} InputChars={InputChars}",
            _model, inputText.Length);

        var messages = BuildMessages(inputText);
        var callOptions = new ChatCompletionOptions
        {
            Temperature = 0f,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "extract_position",
                BinaryData.FromString(ExtractionJsonSchema),
                jsonSchemaIsStrict: true),
        };

        ChatCompletion completion;
        try
        {
            completion = await _chatClient.CompleteChatAsync(messages, callOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(4003, "JdExtractionFailed"), ex,
                "OpenAI JD extraction failed. Model={Model}", _model);
            throw;
        }

        LlmExtractionResponse parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<LlmExtractionResponse>(
                completion.Content[0].Text, JsonOptions)
                ?? throw new InvalidOperationException("OpenAI returned null extraction JSON.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                new EventId(4004, "JdExtractionParseFailed"), ex,
                "Failed to parse OpenAI extraction response.");
            throw;
        }

        _logger.LogInformation(
            new EventId(4002, "JdExtractionCompleted"),
            "JD extraction completed. Title={Title} RequiredSkills={SkillCount} " +
            "InputTokens={InputTokens} OutputTokens={OutputTokens}",
            parsed.Title, parsed.RequiredSkills.Count,
            completion.Usage.InputTokenCount, completion.Usage.OutputTokenCount);

        return MapResult(parsed, inputText.Length);
    }

    private static List<ChatMessage> BuildMessages(string jobDescriptionText)
    {
        var systemPrompt = """
            You are an expert HR assistant that extracts structured information from job descriptions.

            Rules:
            - Extract only information EXPLICITLY stated or clearly implied in the text.
            - For country: extract only if a specific country or city is mentioned. "Remote" alone → null.
            - For seniority_level: use only the allowed enum values. If ambiguous, return null.
              Infer from years of experience only if explicit (e.g. "5+ years" → "Senior").
            - For skills: extract only skills mentioned in the text. Do NOT add related or implied technologies.
              For each skill, include a verbatim quote from the source text as evidence.
            - For description: remove HR boilerplate ("competitive salary", "great team", "we offer...").
              Keep technical requirements, responsibilities, and context.
            - For missing_information: list fields absent from the JD using only the allowed enum values.
            - For confidence fields: High = explicitly stated, Low = inferred indirectly, NotDetected = absent.
            """;

        var userPrompt = $"""
            Extract structured position data from this job description:

            <job_description>
            {jobDescriptionText}
            </job_description>

            Return a JSON object per the provided schema.
            """;

        return
        [
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(userPrompt),
        ];
    }

    private JobDescriptionExtractionResult MapResult(LlmExtractionResponse r, int inputCharCount) =>
        new(
            Title: r.Title,
            Description: r.Description,
            Country: r.Country,
            SeniorityLevel: r.SeniorityLevel,
            RequiredSkills: r.RequiredSkills
                .Select(s => new ExtractedSkill(s.Name, s.Evidence))
                .ToList(),
            NiceToHaveSkills: r.NiceToHaveSkills
                .Select(s => new ExtractedSkill(s.Name, s.Evidence))
                .ToList(),
            Confidence: new ExtractionConfidence(
                Country: ParseConfidence(r.CountryConfidence),
                Seniority: ParseConfidence(r.SeniorityConfidence),
                Skills: ParseConfidence(r.SkillsConfidence)),
            MissingInformation: r.MissingInformation,
            Metadata: new ExtractionMetadata(
                Model: _model,
                PromptVersion: PromptVersion,
                ExtractedAt: DateTimeOffset.UtcNow,
                InputCharCount: inputCharCount));

    private static DetectionConfidence ParseConfidence(string raw) =>
        raw switch
        {
            "High"        => DetectionConfidence.High,
            "Low"         => DetectionConfidence.Low,
            "NotDetected" => DetectionConfidence.NotDetected,
            _             => DetectionConfidence.NotDetected,
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    // strict: true requires all properties in required[] and additionalProperties: false.
    private const string ExtractionJsonSchema = """
        {
          "type": "object",
          "properties": {
            "title":       { "type": "string" },
            "description": { "type": "string" },
            "country":     { "type": ["string", "null"] },
            "seniority_level": {
              "type": ["string", "null"],
              "enum": ["Junior", "Middle", "Senior", "Lead", "Principal", null]
            },
            "required_skills": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name":     { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["name", "evidence"],
                "additionalProperties": false
              }
            },
            "nice_to_have_skills": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name":     { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["name", "evidence"],
                "additionalProperties": false
              }
            },
            "country_confidence":   { "type": "string", "enum": ["High", "Low", "NotDetected"] },
            "seniority_confidence": { "type": "string", "enum": ["High", "Low", "NotDetected"] },
            "skills_confidence":    { "type": "string", "enum": ["High", "Low", "NotDetected"] },
            "missing_information": {
              "type": "array",
              "items": {
                "type": "string",
                "enum": ["Country", "Seniority", "Salary", "WorkingArrangement", "ContractType", "TeamSize"]
              }
            }
          },
          "required": [
            "title", "description", "country", "seniority_level",
            "required_skills", "nice_to_have_skills",
            "country_confidence", "seniority_confidence", "skills_confidence",
            "missing_information"
          ],
          "additionalProperties": false
        }
        """;

    // Internal DTO for deserialising the LLM JSON response.
    private sealed record LlmExtractionResponse(
        string Title,
        string Description,
        string? Country,
        [property: JsonPropertyName("seniority_level")]    string? SeniorityLevel,
        [property: JsonPropertyName("required_skills")]    List<LlmSkill> RequiredSkills,
        [property: JsonPropertyName("nice_to_have_skills")] List<LlmSkill> NiceToHaveSkills,
        [property: JsonPropertyName("country_confidence")]   string CountryConfidence,
        [property: JsonPropertyName("seniority_confidence")] string SeniorityConfidence,
        [property: JsonPropertyName("skills_confidence")]    string SkillsConfidence,
        [property: JsonPropertyName("missing_information")]  List<string> MissingInformation);

    private sealed record LlmSkill(string Name, string Evidence);
}
