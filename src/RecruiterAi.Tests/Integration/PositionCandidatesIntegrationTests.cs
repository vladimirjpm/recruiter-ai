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

// Integration tests for the PositionCandidate junction:
//   - upload auto-attach,
//   - GET /positions/{id}/candidates scope,
//   - idempotent POST attach,
//   - cascade delete from Candidate.
//
// Reuses the WebApplicationFactory pattern from CandidatesIntegrationTests:
// in-memory DB + stub parser + isolated uploads dir.
public class PositionCandidatesIntegrationTests : IClassFixture<PositionCandidatesWebAppFactory>
{
    private readonly PositionCandidatesWebAppFactory _factory;
    private readonly HttpClient _client;

    private static readonly byte[] ValidPdfBytes =
    [
        (byte)'%', (byte)'P', (byte)'D', (byte)'F',
        (byte)'-', (byte)'1', (byte)'.', (byte)'4',
    ];

    public PositionCandidatesIntegrationTests(PositionCandidatesWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── GET /positions/{id}/candidates ────────────────────────────────────────

    [Fact]
    public async Task GetCandidates_UnknownPosition_Returns404()
    {
        var response = await _client.GetAsync($"/api/positions/{Guid.NewGuid()}/candidates");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCandidates_NoAttachments_ReturnsEmptyPage()
    {
        var positionId = await CreatePositionAsync();

        var response = await _client.GetAsync($"/api/positions/{positionId}/candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<CandidatesPage>();
        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task GetCandidates_ReturnsOnlyAttachedToThisPosition()
    {
        var positionA = await CreatePositionAsync("Position A");
        var positionB = await CreatePositionAsync("Position B");

        // Upload one candidate attached to A and another attached to B.
        var candidateA = await UploadCandidateAsync(positionA, "alice.pdf");
        var candidateB = await UploadCandidateAsync(positionB, "bob.pdf");

        var pageA = await GetAttachedAsync(positionA);
        var pageB = await GetAttachedAsync(positionB);

        Assert.Equal(1, pageA.Total);
        Assert.Single(pageA.Items);
        Assert.Equal(candidateA, pageA.Items[0].Id);

        Assert.Equal(1, pageB.Total);
        Assert.Single(pageB.Items);
        Assert.Equal(candidateB, pageB.Items[0].Id);
    }

    // ── POST upload?positionId= attaches with SourceContext=Uploaded ──────────

    [Fact]
    public async Task Upload_WithPositionId_AttachesWithSourceContextUploaded()
    {
        var positionId  = await CreatePositionAsync();
        var candidateId = await UploadCandidateAsync(positionId, "alice.pdf");

        var page = await GetAttachedAsync(positionId);

        Assert.Single(page.Items);
        var item = page.Items[0];
        Assert.Equal(candidateId, item.Id);
        Assert.Equal("Uploaded", item.AttachSourceContext);
    }

    [Fact]
    public async Task Upload_WithUnknownPositionId_Returns404()
    {
        using var content = BuildUpload([(ValidPdfBytes, "alice.pdf", "application/pdf")]);

        var response = await _client.PostAsync(
            $"/api/candidates/upload?positionId={Guid.NewGuid()}", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutPositionId_DoesNotAttach()
    {
        var positionId = await CreatePositionAsync();

        // Upload without ?positionId — candidate is created but not attached.
        using var content = BuildUpload([(ValidPdfBytes, "loose.pdf", "application/pdf")]);
        var response = await _client.PostAsync("/api/candidates/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await GetAttachedAsync(positionId);
        Assert.Empty(page.Items);
    }

    // ── POST /positions/{id}/candidates/{cid} — idempotent attach ─────────────

    [Fact]
    public async Task Attach_NewPair_Returns201()
    {
        var positionId  = await CreatePositionAsync();
        var candidateId = await CreateOrphanCandidateAsync();

        var response = await _client.PostAsync(
            $"/api/positions/{positionId}/candidates/{candidateId}", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(link);
        Assert.Equal("ManuallyAttached", link!.SourceContext);
    }

    [Fact]
    public async Task Attach_RepeatedCall_IsIdempotent()
    {
        var positionId  = await CreatePositionAsync();
        var candidateId = await CreateOrphanCandidateAsync();

        var first  = await _client.PostAsync(
            $"/api/positions/{positionId}/candidates/{candidateId}", content: null);
        var second = await _client.PostAsync(
            $"/api/positions/{positionId}/candidates/{candidateId}", content: null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstLink  = await first .Content.ReadFromJsonAsync<LinkResponse>();
        var secondLink = await second.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.Equal(firstLink!.Id, secondLink!.Id);

        // Junction must contain exactly one row for this pair.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.PositionCandidates
            .CountAsync(pc => pc.PositionId == positionId && pc.CandidateId == candidateId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Attach_UnknownPosition_Returns404()
    {
        var candidateId = await CreateOrphanCandidateAsync();
        var response    = await _client.PostAsync(
            $"/api/positions/{Guid.NewGuid()}/candidates/{candidateId}", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Attach_UnknownCandidate_Returns404()
    {
        var positionId = await CreatePositionAsync();
        var response   = await _client.PostAsync(
            $"/api/positions/{positionId}/candidates/{Guid.NewGuid()}", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Cascade delete configuration ──────────────────────────────────────────
    //
    // EF Core InMemory provider does not execute FK cascade — it only cascades when
    // children are loaded into the same DbContext (which CandidatesController.Delete
    // does not do; it uses FindAsync). Real Postgres applies cascade at the DB level
    // via the migration's ON DELETE CASCADE. To stay provider-agnostic we assert
    // the EF model metadata instead: both FKs on PositionCandidate must be Cascade.
    [Fact]
    public void PositionCandidate_BothForeignKeys_AreConfiguredAsCascade()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entityType = db.Model.FindEntityType(typeof(PositionCandidate));
        Assert.NotNull(entityType);

        foreach (var fk in entityType!.GetForeignKeys())
        {
            Assert.Equal(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade, fk.DeleteBehavior);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreatePositionAsync(string title = "Backend Engineer")
    {
        var dto = new
        {
            Title            = title,
            Description      = "We are looking for a skilled professional.",
            Country          = "remote",
            SeniorityLevel   = "Mid",
            RequiredSkills   = new[] { "C#" },
            NiceToHaveSkills = new[] { "Docker" },
        };
        var response = await _client.PostAsJsonAsync("/api/positions", dto);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private async Task<Guid> UploadCandidateAsync(Guid positionId, string fileName)
    {
        using var content = BuildUpload([(ValidPdfBytes, fileName, "application/pdf")]);
        var response = await _client.PostAsync(
            $"/api/candidates/upload?positionId={positionId}", content);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        return doc.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    // Inserts a Candidate directly through the DbContext, bypassing the upload pipeline.
    // Tests for the manual-attach endpoint should not depend on file storage / parser stubs.
    private async Task<Guid> CreateOrphanCandidateAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var candidate = new Candidate
        {
            Id         = Guid.NewGuid(),
            Name       = "Orphan",
            FileName   = "orphan.pdf",
            RawText    = "Synthetic candidate created directly in tests.",
            Source     = CandidateSource.Uploaded,
            Language   = "en",
            UploadedAt = DateTimeOffset.UtcNow,
        };
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();
        return candidate.Id;
    }

    private async Task<CandidatesPage> GetAttachedAsync(Guid positionId)
    {
        var response = await _client.GetAsync($"/api/positions/{positionId}/candidates");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CandidatesPage>())!;
    }

    private static MultipartFormDataContent BuildUpload(
        (byte[] Data, string FileName, string ContentType)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (data, fileName, contentType) in files)
        {
            var part = new ByteArrayContent(data);
            part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(part, "files", fileName);
        }
        return form;
    }

    // Minimal shapes — only fields the tests assert on.
    private record CandidatesPage(List<AttachedCandidate> Items, int Total, int Offset, int Limit);
    private record AttachedCandidate(Guid Id, string AttachSourceContext);
    private record LinkResponse(Guid Id, Guid PositionId, Guid CandidateId, string SourceContext);
}

// Same shape as CandidatesWebAppFactory but stripped to what these tests need.
// Kept as its own class so xUnit gives each test class an isolated in-memory DB
// (otherwise the IClassFixture would be shared across both test classes and
// candidate ids would leak between them).
public class PositionCandidatesWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _tempUploads =
        Path.Combine(Path.GetTempPath(), $"recruiter-ai-test-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres",
            "Host=localhost;Database=test_placeholder");
        builder.UseSetting("Storage:UploadsPath", _tempUploads);

        var dbName = $"TestDb-PositionCandidates-{Guid.NewGuid()}";

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

            // PositionsController needs IJobDescriptionExtractorService for DI even
            // though tests don't call /extract.
            ReplaceSingleton<IJobDescriptionExtractorService, NoopJdExtractor>(services);

            // CandidatesController parses uploaded PDFs — stub keeps tests free of real PDF structure.
            ReplaceScoped<ICvParserService, FixedTextCvParser>(services);
        });
    }

    private static void ReplaceSingleton<TService, TImpl>(IServiceCollection services)
        where TService : class where TImpl : class, TService
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (existing is not null) services.Remove(existing);
        services.AddSingleton<TService, TImpl>();
    }

    private static void ReplaceScoped<TService, TImpl>(IServiceCollection services)
        where TService : class where TImpl : class, TService
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (existing is not null) services.Remove(existing);
        services.AddScoped<TService, TImpl>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_tempUploads))
            Directory.Delete(_tempUploads, recursive: true);
        base.Dispose(disposing);
    }
}

internal sealed class NoopJdExtractor : IJobDescriptionExtractorService
{
    public Task<JobDescriptionExtractionResult> ExtractAsync(
        string jobDescriptionText, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Not used in PositionCandidate tests.");
}

internal sealed class FixedTextCvParser : ICvParserService
{
    public Task<string> ParseAsync(Stream pdfStream, CancellationToken ct = default)
        => Task.FromResult(
            "Sample candidate resume text extracted from PDF. " +
            "John Doe, Software Engineer with 5 years of experience.");
}
