using RecruiterAi.Domain.Enums;

namespace RecruiterAi.Domain.Entities;

public class Evaluation
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public Guid PositionId { get; set; }
    public Position Position { get; set; } = null!;

    /// <summary>
    /// Final score in the 0–100 range.
    /// </summary>
    public int Score { get; set; }

    public MatchLevel MatchLevel { get; set; }

    /// <summary>
    /// Human-readable explanation of the score, produced by the LLM.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    public List<string> Strengths { get; set; } = [];
    public List<string> Weaknesses { get; set; } = [];
    public List<string> MatchedSkills { get; set; } = [];
    public List<string> MissingSkills { get; set; } = [];
    public List<string> RedFlags { get; set; } = [];
    public List<string> InterviewQuestions { get; set; } = [];

    /// <summary>
    /// OpenAI model identifier that produced this evaluation (e.g. "gpt-4o-mini").
    /// </summary>
    public string AiModel { get; set; } = string.Empty;

    // ── Audit / reproducibility fields (required) ────────────────────────────
    // Always populated so any evaluation can be re-run or compared later.

    /// <summary>
    /// Identifier of the prompt template used (e.g. "v1", "v2-domain-agnostic").
    /// Lets us A/B compare scoring quality across prompt iterations.
    /// </summary>
    public string PromptVersion { get; set; } = string.Empty;

    /// <summary>
    /// Version of the expected JSON response schema.
    /// Increment when the LLM contract changes to avoid parsing stale responses.
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Sampling temperature used for this call.
    /// Production runs at 0 for deterministic scoring; non-zero values in tests.
    /// </summary>
    public decimal Temperature { get; set; }

    /// <summary>
    /// Wall-clock time the OpenAI call took, in milliseconds.
    /// </summary>
    public int EvaluationDurationMs { get; set; }

    // ── Cost tracking fields (nullable) ──────────────────────────────────────
    // Populated once the OpenAI client is wired up (Stage 4).
    // Null = evaluation predates token tracking or ran in a stub.

    /// <summary>
    /// Score broken down by category (e.g. skills, experience, education).
    /// Null until the evaluator is implemented in Stage 4.
    /// </summary>
    public Dictionary<string, int>? ScoreBreakdown { get; set; }

    /// <summary>Number of prompt tokens sent to OpenAI.</summary>
    public int? InputTokens { get; set; }

    /// <summary>Number of completion tokens returned by OpenAI.</summary>
    public int? OutputTokens { get; set; }

    /// <summary>
    /// Estimated USD cost calculated from token counts and model pricing.
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// SHA-256 hex hash of the CV text and position description used as input.
    /// Proves that a score was produced from a specific version of the content —
    /// useful when a candidate edits their CV or a position description changes.
    /// Nullable: null for evaluations created before this field was introduced.
    /// </summary>
    public string? InputHash { get; set; }
}
