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
}
