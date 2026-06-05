using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecruiterAi.Api;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Tests.Integration;

public class CandidatesIntegrationTests : IClassFixture<CandidatesWebAppFactory>
{
    private readonly HttpClient _client;

    // Minimal PDF: magic bytes + version string.
    // The stub parser ignores actual content so no further PDF structure is needed.
    private static readonly byte[] ValidPdfBytes =
    [
        (byte)'%', (byte)'P', (byte)'D', (byte)'F',
        (byte)'-', (byte)'1', (byte)'.', (byte)'4',
    ];

    public CandidatesIntegrationTests(CandidatesWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Upload — rejection ────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_WrongExtension_Returns422()
    {
        using var content = BuildUpload([(ValidPdfBytes, "cv.txt", "application/pdf")]);

        var response = await _client.PostAsync("/api/candidates/upload", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_InvalidMagicBytes_Returns422()
    {
        var badBytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        using var content = BuildUpload([(badBytes, "cv.pdf", "application/pdf")]);

        var response = await _client.PostAsync("/api/candidates/upload", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_TooManyFiles_Returns422()
    {
        // 11 files exceeds the 10-file limit
        var files = Enumerable.Range(0, 11)
            .Select(i => (ValidPdfBytes, $"cv{i}.pdf", "application/pdf"))
            .ToArray();
        using var content = BuildUpload(files);

        var response = await _client.PostAsync("/api/candidates/upload", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_OversizedFile_Returns422()
    {
        // 5 MB + 1 byte — just over the per-file limit
        var big = new byte[5 * 1024 * 1024 + 1];
        big[0] = (byte)'%'; big[1] = (byte)'P'; big[2] = (byte)'D'; big[3] = (byte)'F';

        using var content = BuildUpload([(big, "big.pdf", "application/pdf")]);

        var response = await _client.PostAsync("/api/candidates/upload", content);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── Upload — success ──────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ValidPdf_Returns200WithCandidateId()
    {
        using var content = BuildUpload([(ValidPdfBytes, "alice.pdf", "application/pdf")]);

        var response = await _client.PostAsync("/api/candidates/upload", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var results = doc.RootElement.EnumerateArray().ToList();

        Assert.Single(results);
        Assert.True(results[0].TryGetProperty("id", out var idProp));
        Assert.NotEqual(Guid.Empty, idProp.GetGuid());
    }

    // ── GET candidates ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCandidates_DoesNotExposeRawText()
    {
        // Upload a candidate first so the list is non-empty
        using var upload = BuildUpload([(ValidPdfBytes, "bob.pdf", "application/pdf")]);
        var uploadResponse = await _client.PostAsync("/api/candidates/upload", upload);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var listResponse = await _client.GetAsync("/api/candidates");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var json = await listResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        foreach (var candidate in doc.RootElement.EnumerateArray())
        {
            Assert.False(candidate.TryGetProperty("rawText", out _),
                "rawText must not be exposed in GET /api/candidates");
            Assert.False(candidate.TryGetProperty("RawText", out _),
                "RawText must not be exposed in GET /api/candidates");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
}

/// <summary>
/// WebApplicationFactory for candidate upload tests.
///
/// Differences from PositionsWebAppFactory:
///   - Sets Storage:UploadsPath to an isolated temp directory.
///   - Replaces PdfPigCvParser with a stub that returns fixed text,
///     so tests don't need real parseable PDF content.
/// </summary>
public class CandidatesWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _tempUploads =
        Path.Combine(Path.GetTempPath(), $"recruiter-ai-test-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres",
            "Host=localhost;Database=test_placeholder");
        builder.UseSetting("Storage:UploadsPath", _tempUploads);

        var dbName = $"TestDb-Candidates-{Guid.NewGuid()}";

        builder.ConfigureServices(services =>
        {
            // Remove Npgsql DbContext (same pattern as PositionsWebAppFactory)
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

            // Replace PdfPig with a stub so tests need no real PDF structure.
            var parserDescriptor = services
                .FirstOrDefault(d => d.ServiceType == typeof(ICvParserService));
            if (parserDescriptor is not null) services.Remove(parserDescriptor);
            services.AddScoped<ICvParserService, StubCvParser>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_tempUploads))
            Directory.Delete(_tempUploads, recursive: true);
        base.Dispose(disposing);
    }
}

// Returns deterministic text so tests can assert on parsed content without real PDFs.
internal sealed class StubCvParser : ICvParserService
{
    public Task<string> ParseAsync(Stream pdfStream, CancellationToken ct = default)
        // Must exceed MinExtractedTextLength (100 chars) in CandidatesController.
        => Task.FromResult("Sample candidate resume text extracted from PDF. John Doe, Software Engineer with 5 years of experience in C# and .NET development.");
}
