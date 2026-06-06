using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RecruiterAi.Api.Logging;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Domain.Enums;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Api.Controllers;

// .NET: [ApiController] + [Route("api/positions/{positionId:guid}")]
[ApiController]
[Route("api/positions/{positionId:guid}")]
public class GeneratorController(
    AppDbContext db,
    ICvGenerationService generationService,
    ILogger<GeneratorController> logger)
    : ControllerBase
{
    // .NET: [HttpPost("generate")]
    [HttpPost("generate")]
    [EnableRateLimiting("openai-cost")]
    public async Task<IActionResult> Generate(
        Guid positionId,
        [FromBody] GenerateRequestDto dto,
        CancellationToken ct)
    {
        var position = await db.Positions.FindAsync([positionId], ct);
        if (position is null) return NotFound();

        // Current scope: Generate → Persist → Evaluate (candidates enter the shared pool
        // and can be screened by the recruiter via POST /api/positions/{id}/screen).
        //
        // TODO Future — Validation Lab may support:
        //   - Download-only mode (generate without persistence)
        //   - Export generated CVs as TXT / PDF / ZIP
        //   - Save-to-database toggle in UI (persist: bool flag on this endpoint)
        var result = await generationService.GenerateAsync(position, dto.Count, ct);

        db.CvGenerationBatches.Add(result.Batch);
        db.Candidates.AddRange(result.Candidates);

        // Attach every generated candidate to this position via the junction.
        // SourceContext=Generated lets the UI surface "Generated" badges later.
        var now = DateTimeOffset.UtcNow;
        foreach (var c in result.Candidates)
        {
            db.PositionCandidates.Add(new PositionCandidate
            {
                Id            = Guid.NewGuid(),
                PositionId    = positionId,
                CandidateId   = c.Id,
                SourceContext = PositionCandidateSource.Generated,
                CreatedAt     = now,
            });
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(LogEvents.GenerationBatchCompleted,
            "Generation batch persisted. BatchId={BatchId} PositionId={PositionId} Count={Count}",
            result.Batch.Id, positionId, result.Candidates.Count);

        return Ok(result.Candidates.Select(c => new GeneratedCandidateDto(
            c.Id,
            c.Name,
            c.Email,
            c.FileName,
            c.Language,
            c.ExpectedFitLevel!,
            c.ExpectedScoreRange!.Min,
            c.ExpectedScoreRange!.Max,
            result.Batch.Id,
            c.UploadedAt)));
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record GenerateRequestDto(
    [Range(1, 30)] int Count = 10);

public record GeneratedCandidateDto(
    Guid Id,
    string Name,
    string? Email,
    string FileName,
    string Language,
    string ExpectedFitLevel,
    int ExpectedScoreMin,
    int ExpectedScoreMax,
    Guid BatchId,
    DateTimeOffset GeneratedAt);
