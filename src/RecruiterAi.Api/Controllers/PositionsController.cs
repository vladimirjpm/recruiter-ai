using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruiterAi.Api.Logging;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Domain.Enums;
using RecruiterAi.Domain.Interfaces;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Api.Controllers;

// .NET: [ApiController] + [Route("api/positions")]
[ApiController]
[Route("api/positions")]
public class PositionsController(
    AppDbContext db,
    ILogger<PositionsController> logger,
    IJobDescriptionExtractorService extractor)
    : ControllerBase
{
    // .NET: [HttpPost("extract")]
    [HttpPost("extract")]
    public async Task<IActionResult> Extract([FromBody] ExtractPositionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.JobDescriptionText) || dto.JobDescriptionText.Length < 100)
            return BadRequest(new { error = "Job description must be at least 100 characters." });

        if (dto.JobDescriptionText.Length > 20_000)
            return BadRequest(new { error = "Job description must not exceed 20 000 characters." });

        JobDescriptionExtractionResult result;
        try
        {
            result = await extractor.ExtractAsync(dto.JobDescriptionText, ct);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { error = "Extraction timed out. Please try again." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "JD extraction failed.");
            return StatusCode(503, new { error = "AI extraction service is unavailable. Please try again." });
        }

        return Ok(ToExtractionDto(result));
    }

    // .NET: [HttpGet]
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var positions = await db.Positions
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PositionSummaryDto(p.Id, p.Title, p.Country, p.SeniorityLevel, p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);

        return Ok(positions);
    }

    // .NET: [HttpGet("{id:guid}")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var p = await db.Positions.FindAsync([id], ct);
        if (p is null) return NotFound();
        return Ok(ToDto(p));
    }

    // .NET: [HttpPost]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertPositionDto dto, CancellationToken ct)
    {
        var position = new Position
        {
            Id            = Guid.NewGuid(),
            Title         = dto.Title,
            Description   = dto.Description,
            Country       = dto.Country,
            SeniorityLevel = dto.SeniorityLevel,
            RequiredSkills   = dto.RequiredSkills,
            NiceToHaveSkills = dto.NiceToHaveSkills,
            CreatedAt     = DateTimeOffset.UtcNow,
        };

        db.Positions.Add(position);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(LogEvents.PositionCreated,
            "Position created. Id={Id} Title={Title}", position.Id, position.Title);

        return CreatedAtAction(nameof(GetById), new { id = position.Id }, ToDto(position));
    }

    // .NET: [HttpPut("{id:guid}")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertPositionDto dto, CancellationToken ct)
    {
        var position = await db.Positions.FindAsync([id], ct);
        if (position is null) return NotFound();

        position.Title            = dto.Title;
        position.Description      = dto.Description;
        position.Country          = dto.Country;
        position.SeniorityLevel   = dto.SeniorityLevel;
        position.RequiredSkills   = dto.RequiredSkills;
        position.NiceToHaveSkills = dto.NiceToHaveSkills;
        position.UpdatedAt        = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(LogEvents.PositionUpdated,
            "Position updated. Id={Id}", position.Id);

        return Ok(ToDto(position));
    }

    // .NET: [HttpDelete("{id:guid}")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var position = await db.Positions.FindAsync([id], ct);
        if (position is null) return NotFound();

        db.Positions.Remove(position);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(LogEvents.PositionDeleted,
            "Position deleted. Id={Id}", id);

        return NoContent();
    }

    // .NET: [HttpGet("{id:guid}/candidates")]
    // Returns candidates attached to a position via the PositionCandidate junction.
    // Pagination: ?limit=&offset= — limit clamped to [1, 200], default 50.
    [HttpGet("{id:guid}/candidates")]
    public async Task<IActionResult> GetCandidates(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken ct)
    {
        var positionExists = await db.Positions.AnyAsync(p => p.Id == id, ct);
        if (!positionExists) return NotFound(new { error = "Position not found." });

        var take = Math.Clamp(limit ?? 50, 1, 200);
        var skip = Math.Max(offset ?? 0, 0);

        var query = db.PositionCandidates
            .AsNoTracking()
            .Where(pc => pc.PositionId == id)
            .OrderByDescending(pc => pc.CreatedAt);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip(skip)
            .Take(take)
            .Join(db.Candidates, pc => pc.CandidateId, c => c.Id, (pc, c) =>
                new PositionCandidateDto(
                    c.Id, c.Name, c.Email, c.FileName, c.StoragePath,
                    c.Language, c.Source.ToString(), c.UploadedAt,
                    pc.SourceContext.ToString(), pc.CreatedAt))
            .ToListAsync(ct);

        return Ok(new PositionCandidatesPageDto(items, total, skip, take));
    }

    // .NET: [HttpPost("{id:guid}/candidates/{candidateId:guid}")]
    // Idempotent attach: returns the existing junction row if one already exists,
    // otherwise creates and returns a new one with SourceContext=ManuallyAttached.
    // Always 200/201 — never 409 — so the frontend can call this unconditionally.
    [HttpPost("{id:guid}/candidates/{candidateId:guid}")]
    public async Task<IActionResult> AttachCandidate(Guid id, Guid candidateId, CancellationToken ct)
    {
        var positionExists = await db.Positions.AnyAsync(p => p.Id == id, ct);
        if (!positionExists) return NotFound(new { error = "Position not found." });

        var candidateExists = await db.Candidates.AnyAsync(c => c.Id == candidateId, ct);
        if (!candidateExists) return NotFound(new { error = "Candidate not found." });

        var existing = await db.PositionCandidates
            .FirstOrDefaultAsync(pc => pc.PositionId == id && pc.CandidateId == candidateId, ct);

        if (existing is not null)
        {
            return Ok(new PositionCandidateLinkDto(
                existing.Id, existing.PositionId, existing.CandidateId,
                existing.SourceContext.ToString(), existing.CreatedAt));
        }

        var link = new PositionCandidate
        {
            Id            = Guid.NewGuid(),
            PositionId    = id,
            CandidateId   = candidateId,
            SourceContext = PositionCandidateSource.ManuallyAttached,
            CreatedAt     = DateTimeOffset.UtcNow,
        };

        db.PositionCandidates.Add(link);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race: another request inserted the same pair between our check and SaveChanges.
            // The unique index makes the result identical either way — re-read and return.
            db.Entry(link).State = EntityState.Detached;
            var raced = await db.PositionCandidates
                .AsNoTracking()
                .FirstAsync(pc => pc.PositionId == id && pc.CandidateId == candidateId, ct);
            return Ok(new PositionCandidateLinkDto(
                raced.Id, raced.PositionId, raced.CandidateId,
                raced.SourceContext.ToString(), raced.CreatedAt));
        }

        return StatusCode(201, new PositionCandidateLinkDto(
            link.Id, link.PositionId, link.CandidateId,
            link.SourceContext.ToString(), link.CreatedAt));
    }

    private static PositionDto ToDto(Position p) => new(
        p.Id, p.Title, p.Description, p.Country, p.SeniorityLevel,
        p.RequiredSkills, p.NiceToHaveSkills, p.CreatedAt, p.UpdatedAt);

    private static ExtractionResultDto ToExtractionDto(JobDescriptionExtractionResult r) => new(
        r.Title,
        r.Description,
        r.Country,
        r.SeniorityLevel,
        r.RequiredSkills.Select(s => new ExtractedSkillDto(s.Name, s.Evidence)).ToList(),
        r.NiceToHaveSkills.Select(s => new ExtractedSkillDto(s.Name, s.Evidence)).ToList(),
        new ExtractionConfidenceDto(
            r.Confidence.Country.ToString(),
            r.Confidence.Seniority.ToString(),
            r.Confidence.Skills.ToString()),
        r.MissingInformation,
        new ExtractionMetadataDto(
            r.Metadata.Model,
            r.Metadata.PromptVersion,
            r.Metadata.ExtractedAt,
            r.Metadata.InputCharCount));
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record UpsertPositionDto(
    [Required, MaxLength(300)] string Title,
    [Required] string Description,
    string? Country,
    string? SeniorityLevel,
    [Required, MinLength(1)] List<string> RequiredSkills,
    List<string> NiceToHaveSkills);

public record PositionDto(
    Guid Id,
    string Title,
    string Description,
    string? Country,
    string? SeniorityLevel,
    List<string> RequiredSkills,
    List<string> NiceToHaveSkills,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record PositionSummaryDto(
    Guid Id,
    string Title,
    string? Country,
    string? SeniorityLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record ExtractPositionDto(
    [Required, MinLength(100), MaxLength(20_000)] string JobDescriptionText);

public record ExtractedSkillDto(string Name, string Evidence);

public record ExtractionConfidenceDto(
    string Country,
    string Seniority,
    string Skills);

public record ExtractionMetadataDto(
    string Model,
    string PromptVersion,
    DateTimeOffset ExtractedAt,
    int InputCharCount);

public record PositionCandidateDto(
    Guid Id,
    string Name,
    string? Email,
    string FileName,
    string? StoragePath,
    string Language,
    string Source,
    DateTimeOffset UploadedAt,
    string AttachSourceContext,
    DateTimeOffset AttachedAt);

public record PositionCandidatesPageDto(
    List<PositionCandidateDto> Items,
    int Total,
    int Offset,
    int Limit);

public record PositionCandidateLinkDto(
    Guid Id,
    Guid PositionId,
    Guid CandidateId,
    string SourceContext,
    DateTimeOffset CreatedAt);

public record ExtractionResultDto(
    string Title,
    string Description,
    string? Country,
    string? SeniorityLevel,
    List<ExtractedSkillDto> RequiredSkills,
    List<ExtractedSkillDto> NiceToHaveSkills,
    ExtractionConfidenceDto Confidence,
    List<string> MissingInformation,
    ExtractionMetadataDto Metadata);
