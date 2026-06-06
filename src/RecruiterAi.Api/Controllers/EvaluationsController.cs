using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    // Hard upper bound on candidates per /screen request — protects OpenAI cost
    // and request latency (each candidate is a sequential 2-10s OpenAI roundtrip).
    // TODO Future: background job workflow with bounded concurrency, partial-failure
    // tracking, and progress polling — would let us lift this limit safely.
    private const int MaxCandidatesPerScreen = 10;

    // .NET: [HttpPost("screen")]
    [HttpPost("screen")]
    [EnableRateLimiting("openai-cost")]
    public async Task<IActionResult> Screen(
        Guid positionId,
        [FromBody] ScreenRequestDto dto,
        CancellationToken ct)
    {
        if (dto.CandidateIds.Count > MaxCandidatesPerScreen)
            return BadRequest(new
            {
                error = $"Too many candidates: {dto.CandidateIds.Count}. " +
                        $"Maximum {MaxCandidatesPerScreen} per request.",
            });

        var position = await db.Positions.FindAsync([positionId], ct);
        if (position is null) return NotFound();

        var candidates = await db.Candidates
            .Where(c => dto.CandidateIds.Contains(c.Id))
            .ToListAsync(ct);

        var missing = dto.CandidateIds.Except(candidates.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
            return NotFound(new { error = "One or more candidates were not found.", ids = missing });

        var results = new List<EvaluationDto>(candidates.Count);

        // Pre-load recruiter overrides for all candidates so the response includes FinalScore.
        var pcByCandidateScreen = await db.PositionCandidates
            .Where(pc => pc.PositionId == positionId && dto.CandidateIds.Contains(pc.CandidateId))
            .ToDictionaryAsync(pc => pc.CandidateId, ct);

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

            // Each screen intentionally appends a new row for full audit trail (model, tokens, cost, reasoning).
            // Deduplication for ranking UI is handled at query time via LatestPerCandidate().
            db.Evaluations.Add(evaluation);
            await db.SaveChangesAsync(ct);

            // IsStale is always false immediately after screening.
            results.Add(ToDto(
                evaluation,
                candidate,
                positionUpdatedAt: null,
                pcByCandidateScreen.GetValueOrDefault(candidate.Id)));
        }

        return Ok(results);
    }

    // .NET: [HttpGet("evaluations")]
    [HttpGet("evaluations")]
    public async Task<IActionResult> List(
        Guid positionId,
        [FromQuery] bool includeHistory = false,
        CancellationToken ct = default)
    {
        var position = await db.Positions.FindAsync([positionId], ct);
        if (position is null) return NotFound();

        var evaluations = await db.Evaluations
            .Where(e => e.PositionId == positionId)
            .Include(e => e.Candidate)
            .ToListAsync(ct);

        // Recruiter overrides live on PositionCandidate (survive re-screening).
        // One row per (positionId, candidateId) — load once and map by candidate id.
        var pcByCandidate = await db.PositionCandidates
            .Where(pc => pc.PositionId == positionId)
            .ToDictionaryAsync(pc => pc.CandidateId, ct);

        var result = includeHistory
            ? evaluations
            : LatestPerCandidate(evaluations);

        // Sort by AI score on the backend (default). Final-score sort happens on the frontend.
        return Ok(result.OrderByDescending(e => e.Score)
            .Select(e => ToDto(
                e,
                e.Candidate,
                position.UpdatedAt,
                pcByCandidate.GetValueOrDefault(e.CandidateId))));
    }

    // .NET: [HttpPatch("candidates/{candidateId:guid}/adjustment")]
    // Recruiter override on top of the AI score. AI score itself is never modified.
    [HttpPatch("candidates/{candidateId:guid}/adjustment")]
    public async Task<IActionResult> UpdateAdjustment(
        Guid positionId,
        Guid candidateId,
        [FromBody] AdjustmentRequestDto dto,
        CancellationToken ct)
    {
        // Range guard — keeps recruiter from drowning the AI signal entirely.
        if (dto.RecruiterAdjustment is < -30 or > 30)
            return BadRequest(new { error = "recruiterAdjustment must be between -30 and +30." });

        var pc = await db.PositionCandidates
            .FirstOrDefaultAsync(x => x.PositionId == positionId && x.CandidateId == candidateId, ct);
        if (pc is null)
            return NotFound(new { error = "Candidate is not attached to this position." });

        // 422: adjustment without any AI baseline is meaningless.
        var latest = await db.Evaluations
            .Where(e => e.PositionId == positionId && e.CandidateId == candidateId)
            .OrderByDescending(e => e.CreatedAt)
            .Include(e => e.Candidate)
            .FirstOrDefaultAsync(ct);
        if (latest is null)
            return UnprocessableEntity(new { error = "No AI evaluation exists for this candidate on this position." });

        if (dto.RecruiterAdjustment == 0)
        {
            // Reset semantics: clear comment / attribution as well so no orphan justification remains.
            pc.RecruiterAdjustment = 0;
            pc.RecruiterComment    = null;
            pc.AdjustedBy          = null;
            pc.AdjustedAt          = null;
        }
        else
        {
            // Non-zero adjustment always requires a fresh comment (spec rule: changes need re-justification).
            if (string.IsNullOrWhiteSpace(dto.RecruiterComment) || dto.RecruiterComment.Trim().Length < 10)
                return BadRequest(new
                {
                    error = "recruiterComment is required and must be at least 10 characters " +
                            "when recruiterAdjustment is non-zero.",
                });

            pc.RecruiterAdjustment = dto.RecruiterAdjustment;
            pc.RecruiterComment    = dto.RecruiterComment.Trim();
            pc.AdjustedBy          = string.IsNullOrWhiteSpace(dto.AdjustedBy) ? null : dto.AdjustedBy.Trim();
            pc.AdjustedAt          = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        // Belt-and-braces audit: adjustment history is not stored in DB for MVP,
        // so the structured log line is the only record of who changed what.
        logger.LogInformation(
            "Recruiter adjustment updated. PositionId={PositionId} CandidateId={CandidateId} " +
            "Adjustment={Adjustment} AdjustedBy={AdjustedBy}",
            positionId, candidateId, pc.RecruiterAdjustment, pc.AdjustedBy ?? "(anonymous)");

        var position = await db.Positions.FindAsync([positionId], ct);
        return Ok(ToDto(latest, latest.Candidate, position?.UpdatedAt, pc));
    }

    // .NET: [HttpGet("evaluations/export")]
    [HttpGet("evaluations/export")]
    public async Task<IActionResult> ExportCsv(
        Guid positionId,
        [FromQuery] bool includeHistory = false,
        CancellationToken ct = default)
    {
        var position = await db.Positions.FindAsync([positionId], ct);
        if (position is null) return NotFound();

        var evaluations = await db.Evaluations
            .Where(e => e.PositionId == positionId)
            .Include(e => e.Candidate)
            .ToListAsync(ct);

        var result = includeHistory
            ? evaluations
            : LatestPerCandidate(evaluations);

        var csv = BuildCsv(result.OrderByDescending(e => e.Score).ToList());
        var bytes = Encoding.UTF8.GetBytes(csv);
        var fileName = $"evaluations-{SanitizeFileName(position.Title)}-{positionId:N}.csv";

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Each candidate may have been screened multiple times; keep only the newest row.
    private static List<Evaluation> LatestPerCandidate(List<Evaluation> evaluations) =>
        evaluations
            .GroupBy(e => e.CandidateId)
            .Select(g => g.MaxBy(e => e.CreatedAt)!)
            .ToList();

    private static EvaluationDto ToDto(
        Evaluation e,
        Candidate c,
        DateTimeOffset? positionUpdatedAt,
        PositionCandidate? pc)
    {
        // FinalScore is computed, never stored — cannot drift from inputs.
        var adjustment = pc?.RecruiterAdjustment ?? 0;
        var finalScore = Math.Clamp(e.Score + adjustment, 0, 100);

        // Stale = adjustment exists but predates the AI score we're showing.
        // Tells the recruiter to re-evaluate whether the comment still applies.
        var isAdjustmentStale =
            pc?.AdjustedAt is { } adjAt && adjAt < e.CreatedAt;

        return new EvaluationDto(
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
            e.PromptVersion,
            e.InputTokens,
            e.OutputTokens,
            e.EstimatedCost,
            e.CreatedAt,
            IsStale: positionUpdatedAt.HasValue && e.CreatedAt < positionUpdatedAt.Value,
            RecruiterAdjustment: adjustment,
            RecruiterComment: pc?.RecruiterComment,
            AdjustedBy: pc?.AdjustedBy,
            AdjustedAt: pc?.AdjustedAt,
            FinalScore: finalScore,
            IsAdjustmentStale: isAdjustmentStale);
    }

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
    // CSV injection guard: Excel/LibreOffice evaluate cells starting with =, +, -, @, \t, \r
    // as formulas — even inside quotes. Prefix a single-quote so the value is treated as text.
    private static string CsvEscape(string value)
    {
        var safe = value.Length > 0 && CsvInjectionChars.Contains(value[0])
            ? "'" + value
            : value;
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }

    private static readonly HashSet<char> CsvInjectionChars = ['=', '+', '-', '@', '\t', '\r'];

    // Strip everything except letters, digits, dash, underscore — keeps the filename
    // safe for Content-Disposition and prevents header injection via CR/LF.
    private static string SanitizeFileName(string value)
    {
        var cleaned = SafeFileNameRegex.Replace(value, "-");
        return string.IsNullOrEmpty(cleaned) ? "untitled" : cleaned;
    }

    private static readonly Regex SafeFileNameRegex = new(@"[^A-Za-z0-9_-]+", RegexOptions.Compiled);
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
    string PromptVersion,
    int? InputTokens,
    int? OutputTokens,
    decimal? EstimatedCost,
    DateTimeOffset CreatedAt,
    // True when position was edited after this evaluation was created.
    bool IsStale = false,
    // ── Recruiter override (computed view over PositionCandidate) ─────────────
    int RecruiterAdjustment = 0,
    string? RecruiterComment = null,
    string? AdjustedBy = null,
    DateTimeOffset? AdjustedAt = null,
    // FinalScore = clamp(Score + RecruiterAdjustment, 0, 100). Never stored.
    int FinalScore = 0,
    // True when adjustment was made before the latest AI re-screen — UI should warn.
    bool IsAdjustmentStale = false);

// Request body for PATCH /api/positions/{positionId}/candidates/{candidateId}/adjustment.
// AdjustedBy is optional (auth not yet wired — once it is, this comes from the token).
public record AdjustmentRequestDto(
    int RecruiterAdjustment,
    string? RecruiterComment,
    string? AdjustedBy);
