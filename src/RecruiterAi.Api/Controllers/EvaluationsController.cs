using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruiterAi.Api.Logging;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Domain.Enums;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Api.Controllers;

// .NET: [ApiController] + [Route("api/positions/{positionId:guid}")]
[ApiController]
[Route("api/positions/{positionId:guid}")]
public class EvaluationsController(
    AppDbContext db,
    IResumeEvaluationService evaluationService,
    ILogger<EvaluationsController> logger)
    : ControllerBase
{
    // .NET: [HttpPost("screen")]
    [HttpPost("screen")]
    public async Task<IActionResult> Screen(
        Guid positionId,
        [FromBody] ScreenRequestDto dto,
        CancellationToken ct)
    {
        var position = await db.Positions.FindAsync([positionId], ct);
        if (position is null) return NotFound();

        var candidates = await db.Candidates
            .Where(c => dto.CandidateIds.Contains(c.Id))
            .ToListAsync(ct);

        var missing = dto.CandidateIds.Except(candidates.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
            return NotFound(new { error = "One or more candidates were not found.", ids = missing });

        var results = new List<EvaluationDto>(candidates.Count);

        foreach (var candidate in candidates)
        {
            Evaluation evaluation;
            try
            {
                evaluation = await evaluationService.EvaluateAsync(candidate, position, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(LogEvents.EvaluationFailed, ex,
                    "Evaluation failed. PositionId={PositionId} CandidateId={CandidateId}",
                    positionId, candidate.Id);
                return StatusCode(502, new
                {
                    error       = "Evaluation service failed.",
                    candidateId = candidate.Id,
                });
            }

            // TODO Future: define re-evaluation behavior — currently creates a new Evaluation every
            // time the same candidate is screened for the same position. Options: keep all history
            // (current), replace the latest, or reject with 409 Conflict if one already exists.
            db.Evaluations.Add(evaluation);
            await db.SaveChangesAsync(ct);

            results.Add(ToDto(evaluation, candidate));
        }

        return Ok(results);
    }

    // .NET: [HttpGet("evaluations")]
    [HttpGet("evaluations")]
    public async Task<IActionResult> List(Guid positionId, CancellationToken ct)
    {
        var positionExists = await db.Positions.AnyAsync(p => p.Id == positionId, ct);
        if (!positionExists) return NotFound();

        var evaluations = await db.Evaluations
            .Where(e => e.PositionId == positionId)
            .Include(e => e.Candidate)
            .OrderByDescending(e => e.Score)
            .ToListAsync(ct);

        return Ok(evaluations.Select(e => ToDto(e, e.Candidate)));
    }

    // .NET: [HttpGet("evaluations/export")]
    [HttpGet("evaluations/export")]
    public async Task<IActionResult> ExportCsv(Guid positionId, CancellationToken ct)
    {
        var position = await db.Positions.FindAsync([positionId], ct);
        if (position is null) return NotFound();

        var evaluations = await db.Evaluations
            .Where(e => e.PositionId == positionId)
            .Include(e => e.Candidate)
            .OrderByDescending(e => e.Score)
            .ToListAsync(ct);

        var csv = BuildCsv(evaluations);
        var bytes = Encoding.UTF8.GetBytes(csv);
        var fileName = $"evaluations-{position.Title.Replace(' ', '-')}-{positionId:N}.csv";

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EvaluationDto ToDto(Evaluation e, Candidate c) => new(
        e.Id,
        c.Id,
        c.Name,
        c.FileName,
        e.Score,
        e.MatchLevel.ToString().ToLowerInvariant(),
        e.Reasoning,
        e.Strengths,
        e.Weaknesses,
        e.MatchedSkills,
        e.MissingSkills,
        e.RedFlags,
        e.InterviewQuestions,
        e.AiModel,
        e.InputTokens,
        e.OutputTokens,
        e.EstimatedCost,
        e.CreatedAt);

    private static string BuildCsv(List<Evaluation> evaluations)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "CandidateName,FileName,Score,MatchLevel,Reasoning," +
            "Strengths,Weaknesses,MatchedSkills,MissingSkills,RedFlags," +
            "InterviewQuestions,AiModel,CreatedAt");

        foreach (var e in evaluations)
        {
            sb.AppendLine(string.Join(',',
                CsvEscape(e.Candidate.Name),
                CsvEscape(e.Candidate.FileName),
                e.Score,
                e.MatchLevel.ToString().ToLowerInvariant(),
                CsvEscape(e.Reasoning),
                CsvEscape(string.Join("; ", e.Strengths)),
                CsvEscape(string.Join("; ", e.Weaknesses)),
                CsvEscape(string.Join("; ", e.MatchedSkills)),
                CsvEscape(string.Join("; ", e.MissingSkills)),
                CsvEscape(string.Join("; ", e.RedFlags)),
                CsvEscape(string.Join("; ", e.InterviewQuestions)),
                CsvEscape(e.AiModel),
                e.CreatedAt.ToString("O")));
        }

        return sb.ToString();
    }

    // Wraps a field in double quotes and escapes any embedded double quotes.
    private static string CsvEscape(string value) =>
        '"' + value.Replace("\"", "\"\"") + '"';
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ScreenRequestDto(
    [Required, MinLength(1)] List<Guid> CandidateIds);

public record EvaluationDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string CandidateFileName,
    int Score,
    string MatchLevel,
    string Reasoning,
    List<string> Strengths,
    List<string> Weaknesses,
    List<string> MatchedSkills,
    List<string> MissingSkills,
    List<string> RedFlags,
    List<string> InterviewQuestions,
    string AiModel,
    int? InputTokens,
    int? OutputTokens,
    decimal? EstimatedCost,
    DateTimeOffset CreatedAt);
