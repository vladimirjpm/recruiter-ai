using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecruiterAi.Api.Logging;
using RecruiterAi.Domain.Entities;
using RecruiterAi.Infrastructure.Persistence;

namespace RecruiterAi.Api.Controllers;

// .NET: [ApiController] + [Route("api/positions")]
[ApiController]
[Route("api/positions")]
public class PositionsController(AppDbContext db, ILogger<PositionsController> logger)
    : ControllerBase
{
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

    private static PositionDto ToDto(Position p) => new(
        p.Id, p.Title, p.Description, p.Country, p.SeniorityLevel,
        p.RequiredSkills, p.NiceToHaveSkills, p.CreatedAt, p.UpdatedAt);
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
