namespace RecruiterAi.Domain.Entities;

public class CvGenerationBatch
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public int RequestedCount { get; set; }

    /// <summary>
    /// Requirements inferred by the LLM from the position's job description.
    /// Stored as JSONB so we can later inspect what the generator "understood"
    /// about the role.
    /// </summary>
    public List<string> InferredRequirements { get; set; } = [];

    /// <summary>
    /// Distribution of candidate types across fit categories
    /// (excellent, good, average, weak, ...). Stored as JSONB.
    /// </summary>
    public Dictionary<string, int> CandidateTypes { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Candidate> GeneratedCandidates { get; set; } = [];
}
