using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Domain.Enums;
using RecruiterAi.Infrastructure.Options;
using RecruiterAi.Infrastructure.Services;

namespace RecruiterAi.Tests.Integration;

/// <summary>
/// Live smoke tests that call the real OpenAI API.
///
/// These tests are excluded from normal CI runs.
/// To run them locally:
///
///   set OPENAI__APIKEY=sk-...
///   dotnet test --filter "Category=OpenAiSmoke"
///
/// Or from PowerShell:
///   $env:OPENAI__APIKEY = "sk-..."
///   dotnet test --filter "Category=OpenAiSmoke"
/// </summary>
[Trait("Category", "OpenAiSmoke")]
public class OpenAiSmokeTests
{
    [Fact]
    public async Task EvaluateAsync_RealCandidate_ReturnsValidScore()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI__APIKEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Smoke tests are opt-in. Silently pass when no key is available.
            // Run with: dotnet test --filter "Category=OpenAiSmoke"
            return;
        }

        var options = Options.Create(new LlmOptions
        {
            Provider = "OpenAI",
            ApiKey   = apiKey,
            Model    = "gpt-4o-mini",
        });

        var service = new OpenAiResumeEvaluationService(options, NullLogger<OpenAiResumeEvaluationService>.Instance);

        var candidate = new Candidate
        {
            Id         = Guid.NewGuid(),
            Name       = "Jane Smith",
            RawText    = """
                Jane Smith — Senior Software Engineer
                10 years of experience in C# and .NET.
                Led a team of 5 engineers at Acme Corp.
                Proficient in ASP.NET Core, Entity Framework, PostgreSQL, Docker and Kubernetes.
                Bachelor's degree in Computer Science.
                """,
            FileName   = "jane-smith.pdf",
            Source     = CandidateSource.Uploaded,
            Language   = "en",
            UploadedAt = DateTimeOffset.UtcNow,
        };

        var position = new Position
        {
            Id               = Guid.NewGuid(),
            Title            = "Senior .NET Developer",
            Description      = "We need an experienced .NET developer to lead backend development.",
            Country          = "Remote",
            SeniorityLevel   = "Senior",
            RequiredSkills   = ["C#", ".NET", "ASP.NET Core", "PostgreSQL"],
            NiceToHaveSkills = ["Kubernetes", "Docker"],
            CreatedAt        = DateTimeOffset.UtcNow,
        };

        var evaluation = await service.EvaluateAsync(candidate, position);

        Assert.InRange(evaluation.Score, 0, 100);
        Assert.NotEmpty(evaluation.Reasoning);
        Assert.NotEmpty(evaluation.InterviewQuestions);
        Assert.Equal(5, evaluation.InterviewQuestions.Count);
        Assert.Equal(candidate.Id, evaluation.CandidateId);
        Assert.Equal(position.Id, evaluation.PositionId);
        Assert.Equal("gpt-4o-mini", evaluation.AiModel);
        Assert.True(evaluation.EvaluationDurationMs > 0);
        Assert.True(evaluation.InputTokens > 0);
        Assert.True(evaluation.OutputTokens > 0);

        // Jane is a strong match — score should be well above Medium threshold (40).
        Assert.True(evaluation.Score >= 60,
            $"Expected Jane Smith (10y .NET exp) to score >= 60, got {evaluation.Score}");
    }
}
