using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Infrastructure.Options;
using RecruiterAi.Infrastructure.Services;

namespace RecruiterAi.Tests.Integration;

/// <summary>
/// Live smoke tests for the CV generator that call the real OpenAI API.
///
/// Excluded from normal CI runs.
/// To run locally:
///   $env:LLM__APIKEY = "sk-..."
///   dotnet test --filter "Category=GeneratorSmoke"
/// </summary>
[Trait("Category", "GeneratorSmoke")]
public class GeneratorSmokeTests
{
    private static readonly string[] ValidFitLevels =
    [
        "excellent_fit", "strong_fit", "medium_fit", "weak_fit",
        "overqualified", "underqualified", "missing_key_requirement",
        "career_switcher", "related_industry", "risk_profile",
    ];

    [Fact]
    public async Task GenerateAsync_RealOpenAi_Returns3ValidCandidates()
    {
        var apiKey = Environment.GetEnvironmentVariable("LLM__APIKEY")
                  ?? Environment.GetEnvironmentVariable("OPENAI__APIKEY");
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var options = Options.Create(new LlmOptions
        {
            Provider = "OpenAI",
            ApiKey   = apiKey,
            Model    = "gpt-4o-mini",
        });

        var service = new OpenAiCvGenerationService(
            options,
            NullLogger<OpenAiCvGenerationService>.Instance);

        var position = new Position
        {
            Id               = Guid.NewGuid(),
            Title            = "Senior .NET Developer",
            Description      = "We need an experienced backend engineer for our platform.",
            Country          = "Remote",
            SeniorityLevel   = "Senior",
            RequiredSkills   = ["C#", ".NET", "ASP.NET Core", "PostgreSQL"],
            NiceToHaveSkills = ["Docker", "Kubernetes"],
            CreatedAt        = DateTimeOffset.UtcNow,
        };

        var result = await service.GenerateAsync(position, count: 3);

        // Batch assertions
        Assert.NotEqual(Guid.Empty, result.Batch.Id);
        Assert.Equal(3, result.Batch.RequestedCount);
        Assert.NotEmpty(result.Batch.InferredRequirements);
        Assert.NotEmpty(result.Batch.CandidateTypes);

        // Candidates assertions
        Assert.Equal(3, result.Candidates.Count);
        Assert.All(result.Candidates, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Email));
            Assert.False(string.IsNullOrWhiteSpace(c.RawText));
            Assert.Contains(c.ExpectedFitLevel, ValidFitLevels);
            Assert.NotNull(c.ExpectedScoreRange);
            Assert.InRange(c.ExpectedScoreRange!.Min, 0, 100);
            Assert.InRange(c.ExpectedScoreRange!.Max, 0, 100);
            Assert.True(c.ExpectedScoreRange!.Min <= c.ExpectedScoreRange!.Max);
        });
    }

    [Fact]
    public async Task GenerateAsync_BusDriver_Domain_WorksWithoutItSkills()
    {
        var apiKey = Environment.GetEnvironmentVariable("LLM__APIKEY")
                  ?? Environment.GetEnvironmentVariable("OPENAI__APIKEY");
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var options = Options.Create(new LlmOptions
        {
            Provider = "OpenAI",
            ApiKey   = apiKey,
            Model    = "gpt-4o-mini",
        });

        var service = new OpenAiCvGenerationService(
            options,
            NullLogger<OpenAiCvGenerationService>.Instance);

        // Domain-agnostic test: a non-IT job
        var position = new Position
        {
            Id               = Guid.NewGuid(),
            Title            = "City Bus Driver",
            Description      = "Drive city buses on scheduled routes. Safety-first culture.",
            Country          = "United States",
            SeniorityLevel   = "Mid",
            RequiredSkills   = ["Class B CDL", "Clean driving record", "Passenger safety"],
            NiceToHaveSkills = ["GPS navigation", "Shift flexibility"],
            CreatedAt        = DateTimeOffset.UtcNow,
        };

        var result = await service.GenerateAsync(position, count: 3);

        Assert.Equal(3, result.Candidates.Count);
        Assert.All(result.Candidates, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.RawText));
            // CVs must not be generic IT templates
            Assert.DoesNotContain("JavaScript", c.RawText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("React", c.RawText, StringComparison.OrdinalIgnoreCase);
        });
    }
}
