using RecruiterAi.Domain.Enums;

namespace RecruiterAi.Domain.Entities;

public class Candidate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>
    /// Original uploaded file name, or a generated identifier for synthetic CVs.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Path to the file in storage (for uploaded CVs). Null for generated CVs.
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Full resume text extracted from PDF or produced by the LLM generator.
    /// </summary>
    public string RawText { get; set; } = string.Empty;

    /// <summary>
    /// Resume language: "en" or "he".
    /// </summary>
    public string Language { get; set; } = "en";

    public CandidateSource Source { get; set; }

    /// <summary>
    /// Only for Source=Generated: expected fit level against the target position.
    /// Used to validate whether the evaluator ranks synthetic CVs correctly.
    /// </summary>
    public string? ExpectedFitLevel { get; set; }

    /// <summary>
    /// Only for Source=Generated: expected score range {Min, Max}.
    /// Stored as JSONB.
    /// </summary>
    public ScoreRange? ExpectedScoreRange { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    // Set when Source=Generated. Null for uploaded CVs.
    public Guid? CvGenerationBatchId { get; set; }
    public CvGenerationBatch? CvGenerationBatch { get; set; }

    // Candidate is not owned by a specific position.
    // The candidate<->position relationship is expressed through Evaluations (M:N).
    public ICollection<Evaluation> Evaluations { get; set; } = [];
    public ICollection<CandidateSection> Sections { get; set; } = [];

    // Junction rows: positions this candidate has been attached to.
    public ICollection<PositionCandidate> PositionCandidates { get; set; } = [];
}

/// <summary>
/// Expected score range for a generated CV.
/// </summary>
public record ScoreRange(int Min, int Max);
