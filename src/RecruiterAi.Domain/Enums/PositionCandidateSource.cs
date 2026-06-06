namespace RecruiterAi.Domain.Enums;

// How a candidate ended up attached to a position.
// Uploaded         — CV PDF was uploaded while this position was selected.
// Generated        — Synthetic CV was generated for this position.
// ManuallyAttached — Pre-existing candidate was attached later (or backfilled from Evaluations).
public enum PositionCandidateSource
{
    Uploaded,
    Generated,
    ManuallyAttached
}
