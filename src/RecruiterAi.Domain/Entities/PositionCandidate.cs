using RecruiterAi.Domain.Enums;

namespace RecruiterAi.Domain.Entities;

// Junction between Position and Candidate.
// Candidates are globally reusable; this row records the fact that a candidate
// has been attached to a specific position (Uploaded, Generated, or ManuallyAttached).
//
// Unique on (PositionId, CandidateId) — attach is idempotent.
// Cascade delete from either side: removing a Position or Candidate also removes the link.
// Evaluations remain the source of truth for screening results and are not affected.
public class PositionCandidate
{
    public Guid Id { get; set; }

    public Guid PositionId { get; set; }
    public Position? Position { get; set; }

    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }

    public PositionCandidateSource SourceContext { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // ── Recruiter override ────────────────────────────────────────────────────
    // Manual adjustment applied on top of the AI score (Evaluation.Score).
    // The AI score is never modified — this captures human override based on
    // context not visible in the CV (interviews, referrals, domain fit).
    // Lives on PositionCandidate (not Evaluation) so it survives re-screening.

    /// <summary>
    /// Recruiter adjustment in the range [-30, +30]. Zero means no override.
    /// FinalScore = clamp(latestEvaluation.Score + RecruiterAdjustment, 0, 100).
    /// </summary>
    public int RecruiterAdjustment { get; set; }

    /// <summary>
    /// Required when RecruiterAdjustment != 0. Justifies the override —
    /// without it the adjustment is unauditable noise.
    /// </summary>
    public string? RecruiterComment { get; set; }

    /// <summary>
    /// Recruiter identity (free-form string for now, replaced by user GUID once auth lands).
    /// </summary>
    public string? AdjustedBy { get; set; }

    /// <summary>
    /// When the adjustment was last applied. Compared against latestEvaluation.CreatedAt
    /// to flag stale adjustments after a re-screen.
    /// </summary>
    public DateTimeOffset? AdjustedAt { get; set; }
}
