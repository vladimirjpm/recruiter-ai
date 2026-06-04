using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;
using RecruiterAi.Api;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Tests.Integration;

// WebApplicationFactory spins up the real ASP.NET Core pipeline with an
// in-memory database swapped in — tests exercise routing, model binding,
// and controller logic without hitting PostgreSQL.
public class PositionsIntegrationTests : IClassFixture<PositionsWebAppFactory>
{
    private readonly HttpClient _client;

    public PositionsIntegrationTests(PositionsWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePosition_ValidRequest_Returns201WithBody()
    {
        var dto = SamplePosition("Software Engineer");

        var response = await _client.PostAsJsonAsync("/api/positions", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PositionResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("Software Engineer", body.Title);
        Assert.Equal("remote", body.Country);
    }

    [Fact]
    public async Task CreatePosition_LocationHeaderPointsToNewResource()
    {
        var response = await _client.PostAsJsonAsync("/api/positions", SamplePosition("Data Analyst"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    // ── Get by id ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ExistingPosition_Returns200()
    {
        var created = await CreateAndRead(SamplePosition("Backend Dev"));

        var response = await _client.GetAsync($"/api/positions/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PositionResponse>();
        Assert.Equal(created.Id, body!.Id);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/positions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_AfterCreatingTwo_ReturnsBoth()
    {
        await _client.PostAsJsonAsync("/api/positions", SamplePosition("Role A"));
        await _client.PostAsJsonAsync("/api/positions", SamplePosition("Role B"));

        var response = await _client.GetAsync("/api/positions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<PositionSummary>>();
        Assert.NotNull(list);
        Assert.True(list.Count >= 2);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePosition_ExistingPosition_Returns200WithUpdatedTitle()
    {
        var created = await CreateAndRead(SamplePosition("Old Title"));
        var updated = SamplePosition("New Title");

        var response = await _client.PutAsJsonAsync($"/api/positions/{created.Id}", updated);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PositionResponse>();
        Assert.Equal("New Title", body!.Title);
    }

    [Fact]
    public async Task UpdatePosition_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/positions/{Guid.NewGuid()}", SamplePosition("X"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePosition_ExistingPosition_Returns204()
    {
        var created = await CreateAndRead(SamplePosition("To Delete"));

        var deleteResponse = await _client.DeleteAsync($"/api/positions/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Confirm gone
        var getResponse = await _client.GetAsync($"/api/positions/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeletePosition_UnknownId_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/positions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePosition_MissingTitle_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/positions", new
        {
            Description      = "We are looking for a skilled professional.",
            Country          = "remote",
            RequiredSkills   = new[] { "C#" },
            NiceToHaveSkills = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePosition_EmptyRequiredSkills_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/positions", new
        {
            Title            = "Software Engineer",
            Description      = "We are looking for a skilled professional.",
            RequiredSkills   = Array.Empty<string>(),
            NiceToHaveSkills = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object SamplePosition(string title) => new
    {
        Title            = title,
        Description      = "We are looking for a skilled professional.",
        Country          = "remote",
        SeniorityLevel   = "Mid",
        RequiredSkills   = new[] { "C#", ".NET" },
        NiceToHaveSkills = new[] { "Docker" },
    };

    private async Task<PositionResponse> CreateAndRead(object dto)
    {
        var response = await _client.PostAsJsonAsync("/api/positions", dto);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PositionResponse>())!;
    }

    // Minimal response shapes — only the fields the tests actually assert on
    private record PositionResponse(Guid Id, string Title, string? Country);
    private record PositionSummary(Guid Id, string Title);
}

/// <summary>
/// Replaces PostgreSQL with an isolated in-memory database per test run.
///
/// Two-step override required:
///   1. UseSetting — prevents AddInfrastructure() from throwing on missing
///      connection string (it validates eagerly at service-registration time).
///   2. ConfigureServices — removes the Npgsql DbContext and adds InMemory.
///
/// Testcontainers + real PostgreSQL can replace this in Stage 2+ when
/// jsonb or pgvector-specific behaviour needs testing.
/// </summary>
public class PositionsWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Satisfies the non-null check in DependencyInjection.cs without
        // opening a real connection — the descriptor is replaced below.
        builder.UseSetting("ConnectionStrings:Postgres",
            "Host=localhost;Database=test_placeholder");

        // Compute the database name once here — not inside the lambda.
        // If the name were computed inside, each new DI scope (= each HTTP request)
        // would get a different Guid, creating an empty database per request.
        var dbName = $"TestDb-{Guid.NewGuid()}";

        builder.ConfigureServices(services =>
        {
            // Remove the Npgsql DbContextOptions added by AddInfrastructure()
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            // Build a dedicated EF internal service provider that knows only InMemory.
            // This avoids the "two providers registered" conflict: EF Core checks for
            // duplicate provider registrations in the service provider it uses internally.
            // UseInternalServiceProvider bypasses the app DI container for EF infrastructure.
            var efServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInternalServiceProvider(efServiceProvider)
                   .UseInMemoryDatabase(dbName));
        });
    }
}
