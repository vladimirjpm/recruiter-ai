using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruiterAi.Api.Logging;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Domain.Enums;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Logging;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Api.Controllers;

// .NET: [ApiController] + [Route("api/candidates")]
[ApiController]
[Route("api/candidates")]
public class CandidatesController(
    AppDbContext db,
    ICvParserService cvParser,
    IConfiguration config,
    ILogger<CandidatesController> logger)
    : ControllerBase
{
    private const int MaxFiles = 10;
    private const long MaxFileSizeBytes = 5L * 1024 * 1024; // 5 MB
    private static readonly byte[] PdfMagicBytes = [(byte)'%', (byte)'P', (byte)'D', (byte)'F'];

    // .NET: [HttpGet]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var candidates = await db.Candidates
            .OrderByDescending(c => c.UploadedAt)
            .Select(c => new CandidateDto(
                c.Id, c.Name, c.Email, c.FileName,
                c.StoragePath, c.Language, c.Source.ToString(), c.UploadedAt))
            .ToListAsync(ct);

        return Ok(candidates);
    }

    // .NET: [HttpDelete("{id:guid}")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var candidate = await db.Candidates.FindAsync([id], ct);
        if (candidate is null) return NotFound();

        if (candidate.StoragePath is not null)
        {
            var uploadRoot = GetUploadRoot();
            // GetFullPath normalises the mixed slashes that come from the stored relative path.
            var absolutePath = Path.GetFullPath(Path.Combine(uploadRoot, candidate.StoragePath));
            if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }
            else
            {
                // Missing file is non-fatal: log and continue with DB deletion.
                logger.LogWarning(
                    "CV file not found on disk during candidate delete. CandidateId={Id} Path={Path}",
                    id, absolutePath);
            }
        }

        db.Candidates.Remove(candidate);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // .NET: [HttpPost("upload")]
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(CancellationToken ct)
    {
        var files = Request.Form.Files;

        logger.LogInformation(LogEvents.CvUploadStarted,
            "CV upload started. FileCount={Count}", files.Count);

        if (files.Count == 0)
            return UnprocessableEntity(new { error = "At least one file is required." });

        if (files.Count > MaxFiles)
        {
            logger.LogWarning(LogEvents.CvUploadRejected,
                "CV upload rejected: too many files. Count={Count} Max={Max}", files.Count, MaxFiles);
            return UnprocessableEntity(new { error = $"Maximum {MaxFiles} files per upload." });
        }

        // Buffer all files into memory and validate before saving any to disk.
        // This avoids partial writes when a later file in the batch is invalid.
        var buffered = new List<(IFormFile File, MemoryStream Data)>(files.Count);
        try
        {
            foreach (var file in files)
            {
                // Check size via Content-Length before reading the stream.
                if (file.Length > MaxFileSizeBytes)
                {
                    logger.LogWarning(LogEvents.CvUploadRejected,
                        "CV upload rejected: file too large. FileName={FN} Size={Size}",
                        file.FileName, file.Length);
                    return UnprocessableEntity(new
                    {
                        error  = "File exceeds the 5 MB per-file limit.",
                        fileName = file.FileName,
                    });
                }

                var ext = Path.GetExtension(file.FileName);
                if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(LogEvents.CvUploadRejected,
                        "CV upload rejected: wrong extension. FileName={FN}", file.FileName);
                    return UnprocessableEntity(new
                    {
                        error    = "Only PDF files are accepted.",
                        fileName = file.FileName,
                    });
                }

                if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning(LogEvents.CvUploadRejected,
                        "CV upload rejected: invalid Content-Type. FileName={FN} ContentType={CT}",
                        file.FileName, file.ContentType);
                    return UnprocessableEntity(new
                    {
                        error    = "Invalid Content-Type — expected application/pdf.",
                        fileName = file.FileName,
                    });
                }

                // Copy to MemoryStream so we can read the stream multiple times
                // (validate magic bytes → parse → save to disk).
                var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                ms.Position = 0;

                // Magic bytes: first 4 bytes must be "%PDF"
                if (ms.Length < 4)
                {
                    ms.Dispose();
                    logger.LogWarning(LogEvents.CvUploadRejected,
                        "CV upload rejected: file too small to be a PDF. FileName={FN}", file.FileName);
                    return UnprocessableEntity(new
                    {
                        error    = "File is too small to be a valid PDF.",
                        fileName = file.FileName,
                    });
                }

                var header = new byte[4];
                _ = await ms.ReadAsync(header, ct);
                if (!header.SequenceEqual(PdfMagicBytes))
                {
                    ms.Dispose();
                    logger.LogWarning(LogEvents.CvUploadRejected,
                        "CV upload rejected: invalid magic bytes. FileName={FN}", file.FileName);
                    return UnprocessableEntity(new
                    {
                        error    = "File does not appear to be a valid PDF (invalid magic bytes).",
                        fileName = file.FileName,
                    });
                }

                ms.Position = 0;
                buffered.Add((file, ms));
            }

            // All files passed validation — now parse and persist.
            var uploadRoot = GetUploadRoot();
            Directory.CreateDirectory(Path.Combine(uploadRoot, "candidates"));

            var results = new List<CandidateUploadResultDto>(buffered.Count);

            foreach (var (file, ms) in buffered)
            {
                var candidateId = Guid.NewGuid();

                string rawText;
                try
                {
                    rawText = await cvParser.ParseAsync(ms, ct);
                    logger.LogInformation(LogEvents.PdfParseSuccess,
                        "PDF parsed successfully. CandidateId={Id} FileFingerprint={Fp}",
                        candidateId, PiiSafe.Fingerprint(file.FileName));
                }
                catch (Exception ex)
                {
                    logger.LogError(LogEvents.PdfParseFailure, ex,
                        "PDF parse failed. CandidateId={Id} FileFingerprint={Fp}",
                        candidateId, PiiSafe.Fingerprint(file.FileName));
                    return UnprocessableEntity(new
                    {
                        error    = "Failed to parse the PDF file.",
                        fileName = file.FileName,
                    });
                }

                // Relative path uses forward slashes as agreed in the ADR.
                var relativePath  = $"candidates/{candidateId}.pdf";
                var absolutePath  = Path.Combine(uploadRoot, "candidates", $"{candidateId}.pdf");

                ms.Position = 0;
                await using (var dest = System.IO.File.Create(absolutePath))
                    await ms.CopyToAsync(dest, ct);

                var candidate = new Candidate
                {
                    Id          = candidateId,
                    Name        = Path.GetFileNameWithoutExtension(file.FileName),
                    FileName    = file.FileName,
                    StoragePath = relativePath,
                    RawText     = rawText,
                    Source      = CandidateSource.Uploaded,
                    Language    = "en",
                    UploadedAt  = DateTimeOffset.UtcNow,
                };

                db.Candidates.Add(candidate);
                await db.SaveChangesAsync(ct);

                results.Add(new CandidateUploadResultDto(candidate.Id, candidate.FileName));
            }

            logger.LogInformation(LogEvents.CvUploadCompleted,
                "CV upload completed. Created={Count}", results.Count);

            return Ok(results);
        }
        finally
        {
            foreach (var (_, ms) in buffered)
                ms.Dispose();
        }
    }

    private string GetUploadRoot() =>
        config["Storage:UploadsPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CandidateDto(
    Guid Id,
    string Name,
    string? Email,
    string FileName,
    string? StoragePath,
    string Language,
    string Source,
    DateTimeOffset UploadedAt);

public record CandidateUploadResultDto(Guid Id, string FileName);
