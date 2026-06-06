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
    // PDFs that extract to less text than this are almost certainly scanned/image-only —
    // sending them to OpenAI wastes tokens and produces meaningless evaluations.
    private const int MinExtractedTextLength = 100;
    private static readonly byte[] PdfMagicBytes = [(byte)'%', (byte)'P', (byte)'D', (byte)'F'];

    // TODO Production: all destructive and OpenAI-cost endpoints below need JWT-based
    // authentication, tenant isolation (candidates/positions scoped per user/org),
    // and audit logging (who deleted/screened what, when). Single-user MVP only.

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

    // .NET: [HttpGet("{id:guid}/file")] — streams the original PDF from local storage.
    // Path traversal is prevented by resolving against the upload root and verifying
    // the result still starts with that root (same pattern as Delete).
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        var candidate = await db.Candidates.FindAsync([id], ct);
        if (candidate is null) return NotFound(new { error = "Candidate not found." });

        if (candidate.StoragePath is null)
            return NotFound(new { error = "No file associated with this candidate." });

        // Reject absolute paths up-front: Path.Combine(root, "/etc/passwd") returns "/etc/passwd"
        // on Linux — the StartsWith check below would still catch it, but failing early is clearer.
        if (Path.IsPathRooted(candidate.StoragePath))
        {
            logger.LogWarning(
                "Rejected absolute StoragePath. CandidateId={Id} StoragePath={Path}",
                id, candidate.StoragePath);
            return BadRequest(new { error = "Invalid file path." });
        }

        var uploadRoot   = Path.GetFullPath(GetUploadRoot());
        var absolutePath = Path.GetFullPath(Path.Combine(uploadRoot, candidate.StoragePath));

        // Prevent path traversal: resolved path must stay inside the upload root.
        // StringComparison.Ordinal is correct on Linux (case-sensitive paths) — using
        // OrdinalIgnoreCase would let "/srv/Uploads/x" match an actual root "/srv/uploads".
        if (!absolutePath.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !absolutePath.Equals(uploadRoot, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Path traversal attempt blocked. CandidateId={Id} StoragePath={Path}",
                id, candidate.StoragePath);
            return BadRequest(new { error = "Invalid file path." });
        }

        if (!System.IO.File.Exists(absolutePath))
        {
            logger.LogWarning(
                "CV file not found on disk. CandidateId={Id} Path={Path}", id, absolutePath);
            return NotFound(new { error = "File not found on disk." });
        }

        var stream = System.IO.File.OpenRead(absolutePath);
        // Omit fileDownloadName so browser gets Content-Disposition: inline and opens the PDF in a new tab.
        return File(stream, "application/pdf", enableRangeProcessing: true);
    }

    // Returns the raw CV text for generated candidates.
    // rawText is intentionally excluded from list endpoints and never logged.
    // .NET: [HttpGet("{id:guid}/resume-text")]
    [HttpGet("{id:guid}/resume-text")]
    public async Task<IActionResult> GetResumeText(Guid id, CancellationToken ct)
    {
        var candidate = await db.Candidates
            .Where(c => c.Id == id)
            .Select(c => new { c.Source, c.RawText })
            .FirstOrDefaultAsync(ct);

        if (candidate is null)
            return NotFound(new { error = "Candidate not found." });

        if (candidate.Source != CandidateSource.Generated)
            return BadRequest(new { error = "resume-text is only available for generated candidates." });

        // rawText is not logged — contains PII (synthetic but resembles real data).
        return Ok(new { text = candidate.RawText });
    }

    // .NET: [HttpDelete("{id:guid}")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var candidate = await db.Candidates.FindAsync([id], ct);
        if (candidate is null) return NotFound();

        if (candidate.StoragePath is not null && !Path.IsPathRooted(candidate.StoragePath))
        {
            var uploadRoot = Path.GetFullPath(GetUploadRoot());
            // GetFullPath normalises the mixed slashes that come from the stored relative path.
            var absolutePath = Path.GetFullPath(Path.Combine(uploadRoot, candidate.StoragePath));

            // Same Ordinal traversal guard as DownloadFile — we are about to call File.Delete.
            var insideRoot =
                absolutePath.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || absolutePath.Equals(uploadRoot, StringComparison.Ordinal);

            if (insideRoot && System.IO.File.Exists(absolutePath))
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
    // Optional ?positionId= attaches every uploaded candidate to that position
    // via the PositionCandidate junction (SourceContext=Uploaded). If the position
    // does not exist, returns 404 before any file is parsed or saved.
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromQuery] Guid? positionId,
        CancellationToken ct)
    {
        if (positionId is { } pid &&
            !await db.Positions.AnyAsync(p => p.Id == pid, ct))
        {
            return NotFound(new { error = "Position not found." });
        }

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

                // Guard against scanned/image-only PDFs: PdfPig returns near-empty text
                // and the OpenAI call would burn tokens producing meaningless output.
                if (rawText.Trim().Length < MinExtractedTextLength)
                {
                    logger.LogWarning(LogEvents.CvUploadRejected,
                        "CV upload rejected: extracted text too short. CandidateId={Id} Length={Len}",
                        candidateId, rawText.Trim().Length);
                    return UnprocessableEntity(new
                    {
                        error    = "PDF contains no readable text — likely a scanned/image-only document. " +
                                   "Please upload a text-based PDF.",
                        fileName = file.FileName,
                    });
                }

                // Relative path uses forward slashes as agreed in the ADR.
                var relativePath  = $"candidates/{candidateId}.pdf";
                var absolutePath  = Path.Combine(uploadRoot, "candidates", $"{candidateId}.pdf");

                ms.Position = 0;
                await using (var dest = System.IO.File.Create(absolutePath))
                    await ms.CopyToAsync(dest, ct);

                // TODO Future: extract candidate name from CV text (contact block / first heading);
                // filename is the fallback for when extraction fails or is not yet implemented.
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

                // TODO Future: duplicate detection warning by normalized email (most reliable)
                // + file hash (exact binary match) or raw_text fingerprint (SHA256 of normalized
                // text — catches renamed files). Name-based matching is unreliable (common surnames).
                db.Candidates.Add(candidate);

                // Attach to the supplied position via the junction. SourceContext=Uploaded
                // distinguishes this from Generated/ManuallyAttached in later UI surfaces.
                if (positionId is { } attachPid)
                {
                    db.PositionCandidates.Add(new PositionCandidate
                    {
                        Id            = Guid.NewGuid(),
                        PositionId    = attachPid,
                        CandidateId   = candidate.Id,
                        SourceContext = PositionCandidateSource.Uploaded,
                        CreatedAt     = candidate.UploadedAt,
                    });
                }

                // TODO Future: cleanup saved file on disk if SaveChangesAsync fails (currently the
                // file is persisted but the DB record is not, leaving an orphaned file).
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
