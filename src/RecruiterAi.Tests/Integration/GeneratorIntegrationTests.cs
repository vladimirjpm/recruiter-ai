using System.Net;
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

public class GeneratorIntegrationTests : IClassFixture<GeneratorWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly GeneratorWebAppFactory _factory;

    public GeneratorIntegrationTests(GeneratorWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    [Fact]
    public async Task Generate_ValidRequest_Returns200WithCandidates()
    {
        var positionId = await CreatePositionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/generate",
            new { count = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.EnumerateArray().ToList();

        Assert.Equal(3, items.Count);
        Assert.All(items, item =>
        {
            Assert.True(item.TryGetProperty("id", out var id));
            Assert.NotEqual(Guid.Empty, id.GetGuid());
            Assert.True(item.TryGetProperty("name", out var name));
            Assert.False(string.IsNullOrWhiteSpace(name.GetString()));
            Assert.True(item.TryGetProperty("expectedFitLevel", out _));
            Assert.True(item.TryGetProperty("batchId", out _));
        });
    }

    [Fact]
    public async Task Generate_DefaultCount_Returns10Candidates()
    {
        var positionId = await CreatePositionAsync();

        // Omitting count — default is 10
        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/generate",
            new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(10, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Generate_CountZero_Returns400()
    {
        var positionId = await CreatePositionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/generate",
            new { count = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_CountOver30_Returns400()
    {
        var positionId = await CreatePositionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/generate",
            new { count = 31 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_UnknownPosition_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{Guid.NewGuid()}/generate",
            new { count = 5 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Generate_PersistsCandidatesAndBatchInDatabase()
    {
        var positionId = await CreatePositionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/generate",
            new { count = 3 });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var batchId = doc.RootElement.EnumerateArray().First()
            .GetProperty("batchId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var batch = await db.CvGenerationBatches.FindAsync(batchId);
        Assert.NotNull(batch);
        Assert.Equal(3, batch.RequestedCount);

        var candidateCount = await db.Candidates
            .CountAsync(c => c.CvGenerationBatchId == batchId);
        Assert.Equal(3, candidateCount);
    }

    [Fact]
    public async Task Generate_CandidatesHaveSourceGenerated()
    {
        var positionId = await CreatePositionAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/generate",
            new { count = 2 });

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var candidates = await db.Candidates
            .Where(c => c.Source == CandidateSource.Generated)
            .ToListAsync();

        Assert.True(candidates.Count >= 2);
        Assert.All(candidates, c =>
        {
            Assert.Equal(CandidateSource.Generated, c.Source);
            Assert.NotNull(c.ExpectedFitLevel);
            Assert.NotNull(c.ExpectedScoreRange);
            Assert.Null(c.StoragePath);
        });
    }

    [Fact]
    public async Task Generate_CandidatesVisibleInCandidatesList()
    {
        var positionId = await CreatePositionAsync();

        await _client.PostAsJsonAsync(
            $"/api/positions/{positionId}/generate",
            new { count = 2 });

        var response = await _client.GetAsync("/api/candidates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var sources = doc.RootElement.EnumerateArray()
            .Select(c => c.GetProperty("source").GetString())
            .ToList();

        Assert.Contains("Generated", sources);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreatePositionAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/positions", new
        {
            Title            = "Software Engineer",
            Description      = "Build great backend systems with .NET and PostgreSQL.",
            Country          = "Remote",
            SeniorityLevel   = "Mid",
            RequiredSkills   = new[] { "C#", ".NET" },
            NiceToHaveSkills = new[] { "Docker" },
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}

/// <summary>
/// WebApplicationFactory for generator tests.
/// Uses InMemory DB and a stub ICvGenerationService — no OpenAI key required.
/// </summary>
public class GeneratorWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _tempUploads =
        Path.Combine(Path.GetTempPath(), $"recruiter-ai-gen-test-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres",
            "Host=localhost;Database=test_placeholder");
        builder.UseSetting("Storage:UploadsPath", _tempUploads);

        var dbName = $"TestDb-Generator-{Guid.NewGuid()}";

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
            Replace<ICvGenerationService, StubCvGenerationService>(services);
            Replace<IJobDescriptionExtractorService, StubJobDescriptionExtractorService>(services);
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

internal sealed class StubCvGenerationService : ICvGenerationService
{
    public Task<CvGenerationResult> GenerateAsync(
        Position position,
        int count,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var batch = new CvGenerationBatch
        {
            Id                   = batchId,
            PositionId           = position.Id,
            RequestedCount       = count,
            InferredRequirements = ["C# .NET experience", "Backend development"],
            CandidateTypes       = new Dictionary<string, int> { ["excellent_fit"] = count },
            CreatedAt            = DateTimeOffset.UtcNow,
        };

        var candidates = Enumerable.Range(1, count).Select(i => new Candidate
        {
            Id                  = Guid.NewGuid(),
            Name                = $"Generated Candidate {i}",
            Email               = $"candidate{i}@example.com",
            FileName            = $"generated_candidate_{i}.txt",
            RawText             = $"Synthetic CV for candidate {i} targeting {position.Title}.",
            Language            = "en",
            Source              = CandidateSource.Generated,
            ExpectedFitLevel    = "excellent_fit",
            ExpectedScoreRange  = new ScoreRange(85, 100),
            UploadedAt          = DateTimeOffset.UtcNow,
            CvGenerationBatchId = batchId,
        }).ToList();

        return Task.FromResult(new CvGenerationResult(batch, candidates));
    }
}
