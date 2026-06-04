using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RecruiterAi.Api.Logging;
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
    public async Task<IActionResult> Generate(
        Guid positionId,
        [FromBody] GenerateRequestDto dto,
        CancellationToken ct)
    {
        var position = await db.Positions.FindAsync([positionId], ct);
        if (position is null) return NotFound();

        var result = await generationService.GenerateAsync(position, dto.Count, ct);

        db.CvGenerationBatches.Add(result.Batch);
        db.Candidates.AddRange(result.Candidates);
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
