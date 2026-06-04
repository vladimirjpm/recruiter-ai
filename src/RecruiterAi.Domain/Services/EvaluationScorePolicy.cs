using RecruiterAi.Domain.Enums;

namespace RecruiterAi.Domain.Services;

/// <summary>
/// Domain rules for evaluation score validation and MatchLevel mapping.
/// Pure static — no dependencies on OpenAI, EF Core, or ASP.NET.
///
/// Thresholds (Strong ≥ 70, Medium ≥ 40, Weak &lt; 40) are intentionally
/// defined here, not in the LLM prompt, so the prompt can evolve independently
/// without changing the ranking behavior visible to recruiters.
/// </summary>
public static class EvaluationScorePolicy
{
    public const int MinScore = 0;
    public const int MaxScore = 100;

    // Thresholds are business rules, not magic numbers:
    // Strong = clear fit, proceed to interview
    // Medium = possible fit, needs closer look
    // Weak   = significant gaps, likely skip
    public const int StrongThreshold = 70;
    public const int MediumThreshold = 40;

    /// <summary>
    /// Returns true if <paramref name="score"/> is within the valid 0–100 range.
    /// </summary>
    public static bool IsValid(int score) =>
        score is >= MinScore and <= MaxScore;

    /// <summary>
    /// Maps a validated score to a <see cref="MatchLevel"/>.
    /// Throws <see cref="ArgumentOutOfRangeException"/> for scores outside 0–100
    /// so callers are forced to validate before mapping.
    /// </summary>
    public static MatchLevel ToMatchLevel(int score)
    {
        if (!IsValid(score))
            throw new ArgumentOutOfRangeException(nameof(score),
                $"Score must be between {MinScore} and {MaxScore}, got {score}.");

        return score switch
        {
            >= StrongThreshold => MatchLevel.Strong,
            >= MediumThreshold => MatchLevel.Medium,
            _                  => MatchLevel.Weak
        };
    }
}
