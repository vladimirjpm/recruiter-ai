using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruiterAi.Api;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Domain.Enums;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Tests.Infrastructure;

// Verifies that EF Core change-tracking correctly detects in-memory mutations
// of Dictionary<string,int> properties that have a ValueConverter.
// Without a matching ValueComparer, SaveChanges() silently drops the update.
public class ValueComparerTests : IClassFixture<ValueComparerWebAppFactory>
{
    private readonly IServiceProvider _services;

    public ValueComparerTests(ValueComparerWebAppFactory factory)
    {
        _services = factory.Services;
    }

    // ── CvGenerationBatch.CandidateTypes ─────────────────────────────────────

    [Fact]
    public async Task CandidateTypes_MutationOnTrackedEntity_IsSavedByEfCore()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var position = new Position
        {
            Title = "Test", Description = "Desc", Country = "IL", SeniorityLevel = "Mid"
        };
        db.Positions.Add(position);
        await db.SaveChangesAsync();

        var batch = new CvGenerationBatch
        {
            PositionId = position.Id,
            RequestedCount = 5,
            CandidateTypes = new Dictionary<string, int> { ["senior"] = 3 }
        };
        db.CvGenerationBatches.Add(batch);
        await db.SaveChangesAsync();

        // Mutate the tracked entity's dictionary
        var tracked = await db.CvGenerationBatches.FindAsync(batch.Id);
        tracked!.CandidateTypes["junior"] = 2;
        await db.SaveChangesAsync();

        // Reload from a fresh context to confirm the mutation was persisted
        using var scope2 = _services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db2.CvGenerationBatches.FindAsync(batch.Id);

        Assert.Equal(2, reloaded!.CandidateTypes.Count);
        Assert.Equal(2, reloaded.CandidateTypes["junior"]);
    }

    [Fact]
    public async Task CandidateTypes_FullReplacement_IsSavedByEfCore()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var position = new Position
        {
            Title = "Test2", Description = "Desc", Country = "IL", SeniorityLevel = "Mid"
        };
        db.Positions.Add(position);
        await db.SaveChangesAsync();

        var batch = new CvGenerationBatch
        {
            PositionId = position.Id,
            RequestedCount = 2,
            CandidateTypes = new Dictionary<string, int> { ["mid"] = 2 }
        };
        db.CvGenerationBatches.Add(batch);
        await db.SaveChangesAsync();

        var tracked = await db.CvGenerationBatches.FindAsync(batch.Id);
        // Replace the entire dictionary reference
        tracked!.CandidateTypes = new Dictionary<string, int> { ["lead"] = 1, ["staff"] = 1 };
        await db.SaveChangesAsync();

        using var scope2 = _services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db2.CvGenerationBatches.FindAsync(batch.Id);

        Assert.False(reloaded!.CandidateTypes.ContainsKey("mid"));
        Assert.Equal(1, reloaded.CandidateTypes["lead"]);
    }

    // ── Evaluation.ScoreBreakdown ─────────────────────────────────────────────

    [Fact]
    public async Task ScoreBreakdown_MutationOnTrackedEntity_IsSavedByEfCore()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (position, candidate) = await SeedPositionAndCandidate(db);

        var evaluation = new Evaluation
        {
            PositionId = position.Id,
            CandidateId = candidate.Id,
            Score = 75,
            MatchLevel = MatchLevel.Strong,
            Reasoning = "Good",
            AiModel = "gpt-4o-mini",
            PromptVersion = "v1",
            SchemaVersion = "v1",
            Temperature = 0m,
            EvaluationDurationMs = 500,
            ScoreBreakdown = new Dictionary<string, int> { ["skills"] = 80 }
        };
        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync();

        var tracked = await db.Evaluations.FindAsync(evaluation.Id);
        tracked!.ScoreBreakdown!["experience"] = 70;
        await db.SaveChangesAsync();

        using var scope2 = _services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db2.Evaluations.FindAsync(evaluation.Id);

        Assert.Equal(2, reloaded!.ScoreBreakdown!.Count);
        Assert.Equal(70, reloaded.ScoreBreakdown["experience"]);
    }

    [Fact]
    public async Task ScoreBreakdown_NullToValue_IsSavedByEfCore()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (position, candidate) = await SeedPositionAndCandidate(db);

        var evaluation = new Evaluation
        {
            PositionId = position.Id,
            CandidateId = candidate.Id,
            Score = 50,
            MatchLevel = MatchLevel.Medium,
            Reasoning = "Ok",
            AiModel = "gpt-4o-mini",
            PromptVersion = "v1",
            SchemaVersion = "v1",
            Temperature = 0m,
            EvaluationDurationMs = 400,
            ScoreBreakdown = null
        };
        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync();

        var tracked = await db.Evaluations.FindAsync(evaluation.Id);
        tracked!.ScoreBreakdown = new Dictionary<string, int> { ["culture"] = 90 };
        await db.SaveChangesAsync();

        using var scope2 = _services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db2.Evaluations.FindAsync(evaluation.Id);

        Assert.NotNull(reloaded!.ScoreBreakdown);
        Assert.Equal(90, reloaded.ScoreBreakdown["culture"]);
    }

    [Fact]
    public async Task ScoreBreakdown_ValueToNull_IsSavedByEfCore()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (position, candidate) = await SeedPositionAndCandidate(db);

        var evaluation = new Evaluation
        {
            PositionId = position.Id,
            CandidateId = candidate.Id,
            Score = 35,
            MatchLevel = MatchLevel.Weak,
            Reasoning = "Weak",
            AiModel = "gpt-4o-mini",
            PromptVersion = "v1",
            SchemaVersion = "v1",
            Temperature = 0m,
            EvaluationDurationMs = 300,
            ScoreBreakdown = new Dictionary<string, int> { ["skills"] = 30 }
        };
        db.Evaluations.Add(evaluation);
        await db.SaveChangesAsync();

        var tracked = await db.Evaluations.FindAsync(evaluation.Id);
        tracked!.ScoreBreakdown = null;
        await db.SaveChangesAsync();

        using var scope2 = _services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db2.Evaluations.FindAsync(evaluation.Id);

        Assert.Null(reloaded!.ScoreBreakdown);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<(Position, Candidate)> SeedPositionAndCandidate(AppDbContext db)
    {
        var position = new Position
        {
            Title = "Dev", Description = "Dev role", Country = "US", SeniorityLevel = "Senior"
        };
        var candidate = new Candidate
        {
            Name = "Test User",
            FileName = "cv.pdf",
            RawText = "Some CV text",
            Language = "en",
            Source = CandidateSource.Uploaded
        };
        db.Positions.Add(position);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();
        return (position, candidate);
    }
}

public class ValueComparerWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres",
            "Host=localhost;Database=test_placeholder");

        var dbName = $"ValueComparerTests_{Guid.NewGuid()}";

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
        });
    }
}
