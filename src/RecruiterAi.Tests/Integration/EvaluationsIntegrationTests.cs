using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruiterAi.Api;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Domain.Enums;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Tests.Integration;

public class EvaluationsIntegrationTests : IClassFixture<EvaluationsWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly EvaluationsWebAppFactory _factory;

    private static readonly byte[] MinimalPdfBytes =
    [
        (byte)'%', (byte)'P', (byte)'D', (byte)'F',
        (byte)'-', (byte)'1', (byte)'.', (byte)'4',
    ];

    public EvaluationsIntegrationTests(EvaluationsWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Screen ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Screen_ValidCandidateAndPosition_Returns200WithEvaluation()
    {
        var positionId  = await CreatePositionAsync();
        var candidateId = await UploadCandidateAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/screen",
            new { candidateIds = new[] { candidateId } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.EnumerateArray().ToList();

        Assert.Single(items);
        Assert.True(items[0].TryGetProperty("id", out var idProp));
        Assert.NotEqual(Guid.Empty, idProp.GetGuid());
        Assert.True(items[0].TryGetProperty("score", out var scoreProp));
        Assert.InRange(scoreProp.GetInt32(), 0, 100);
    }

    [Fact]
    public async Task Screen_UnknownPosition_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{Guid.NewGuid()}/screen",
            new { candidateIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Screen_EmptyCandidateIds_Returns422()
    {
        var positionId = await CreatePositionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/screen",
            new { candidateIds = Array.Empty<Guid>() });

        // [MinLength(1)] on the DTO triggers [ApiController] model validation → 400
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Screen_UnknownCandidate_Returns404()
    {
        var positionId = await CreatePositionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/screen",
            new { candidateIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Screen_PersistsEvaluationInDatabase()
    {
        var positionId  = await CreatePositionAsync();
        var candidateId = await UploadCandidateAsync();

        await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/screen",
            new { candidateIds = new[] { candidateId } });

        // Verify the evaluation was actually saved
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Evaluations.CountAsync(
            e => e.PositionId == positionId && e.CandidateId == candidateId);

        Assert.Equal(1, count);
    }

    // ── List evaluations ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvaluations_SortedByScoreDescending()
    {
        var positionId = await CreatePositionAsync();

        // Seed evaluations directly with different scores
        await SeedEvaluationAsync(positionId, score: 30);
        await SeedEvaluationAsync(positionId, score: 85);
        await SeedEvaluationAsync(positionId, score: 55);

        var response = await _client.GetAsync($"/api/positions/{positionId}/evaluations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var scores = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("score").GetInt32())
            .ToList();

        Assert.Equal(scores.OrderByDescending(s => s).ToList(), scores);
    }

    [Fact]
    public async Task GetEvaluations_UnknownPosition_Returns404()
    {
        var response = await _client.GetAsync($"/api/positions/{Guid.NewGuid()}/evaluations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── CSV export ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportCsv_Returns200WithCsvContentType()
    {
        var positionId  = await CreatePositionAsync();
        var candidateId = await UploadCandidateAsync();

        await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/screen",
            new { candidateIds = new[] { candidateId } });

        var response = await _client.GetAsync($"/api/positions/{positionId}/evaluations/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType?.ToString());

        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("CandidateName", csv);
        Assert.Contains("Score", csv);
    }

    [Fact]
    public async Task ExportCsv_UnknownPosition_Returns404()
    {
        var response = await _client.GetAsync(
            $"/api/positions/{Guid.NewGuid()}/evaluations/export");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreatePositionAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/positions", new
        {
            Title            = "Software Engineer",
            Description      = "Build great things.",
            Country          = "Remote",
            SeniorityLevel   = "Mid",
            RequiredSkills   = new[] { "C#", ".NET" },
            NiceToHaveSkills = new[] { "Docker" },
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task<Guid> UploadCandidateAsync()
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(MinimalPdfBytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(part, "files", "candidate.pdf");

        var response = await _client.PostAsync("/api/candidates/upload", form);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    // Seeds an evaluation directly into the DB to avoid OpenAI dependency.
    private async Task SeedEvaluationAsync(Guid positionId, int score)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var candidate = new Candidate
        {
            Id         = Guid.NewGuid(),
            Name       = $"Candidate score={score}",
            FileName   = "cv.pdf",
            RawText    = "sample",
            Source     = CandidateSource.Uploaded,
            Language   = "en",
            UploadedAt = DateTimeOffset.UtcNow,
        };
        db.Candidates.Add(candidate);

        var evaluation = new Evaluation
        {
            Id                   = Guid.NewGuid(),
            CandidateId          = candidate.Id,
            PositionId           = positionId,
            Score                = score,
            MatchLevel           = score >= 70 ? MatchLevel.Strong
                                 : score >= 40 ? MatchLevel.Medium
                                 :               MatchLevel.Weak,
            Reasoning            = $"Score {score} reasoning.",
            AiModel              = "stub",
            PromptVersion        = "v1",
            SchemaVersion        = "v1",
            EvaluationDurationMs = 0,
            CreatedAt            = DateTimeOffset.UtcNow,
        };
        db.Evaluations.Add(evaluation);

        await db.SaveChangesAsync();
    }
}

/// <summary>
/// WebApplicationFactory for evaluation tests.
///
/// Replaces both ICvParserService and IResumeEvaluationService with stubs so
/// that tests run without a real PDF parser or OpenAI key.
/// </summary>
public class EvaluationsWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _tempUploads =
        Path.Combine(Path.GetTempPath(), $"recruiter-ai-eval-test-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres",
            "Host=localhost;Database=test_placeholder");
        builder.UseSetting("Storage:UploadsPath", _tempUploads);

        var dbName = $"TestDb-Evaluations-{Guid.NewGuid()}";

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            var efServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInternalServiceProvider(efServiceProvider)
                   .UseInMemoryDatabase(dbName));

            Replace<ICvParserService, StubCvParser>(services);
            Replace<IResumeEvaluationService, StubResumeEvaluationService>(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_tempUploads))
            Directory.Delete(_tempUploads, recursive: true);
        base.Dispose(disposing);
    }

    private static void Replace<TService, TImplementation>(IServiceCollection services)
        where TImplementation : class, TService
        where TService : class
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (existing is not null) services.Remove(existing);
        services.AddScoped<TService, TImplementation>();
    }
}

internal sealed class StubResumeEvaluationService : IResumeEvaluationService
{
    public Task<Evaluation> EvaluateAsync(
        Candidate candidate,
        Position position,
        CancellationToken cancellationToken = default)
    {
        var evaluation = new Evaluation
        {
            Id                   = Guid.NewGuid(),
            CandidateId          = candidate.Id,
            PositionId           = position.Id,
            Score                = 75,
            MatchLevel           = MatchLevel.Strong,
            Reasoning            = "Stub evaluation: candidate is a strong match.",
            Strengths            = ["Strong .NET background", "Relevant experience"],
            Weaknesses           = ["Limited cloud experience"],
            MatchedSkills        = position.RequiredSkills.Take(2).ToList(),
            MissingSkills        = [],
            RedFlags             = [],
            InterviewQuestions   = ["Q1?", "Q2?", "Q3?", "Q4?", "Q5?"],
            AiModel              = "stub",
            PromptVersion        = "v1",
            SchemaVersion        = "v1",
            Temperature          = 0m,
            EvaluationDurationMs = 0,
            InputTokens          = null,
            OutputTokens         = null,
            EstimatedCost        = null,
            CreatedAt            = DateTimeOffset.UtcNow,
        };
        return Task.FromResult(evaluation);
    }
}
