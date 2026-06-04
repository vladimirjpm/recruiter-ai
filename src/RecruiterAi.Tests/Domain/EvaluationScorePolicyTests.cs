using RecruiterAi.Domain.Enums;
using RecruiterAi.Domain.Services;

namespace RecruiterAi.Tests.Domain;

public class EvaluationScorePolicyTests
{
    // ── IsValid ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(101)]
    [InlineData(999)]
    public void IsValid_OutOfRange_ReturnsFalse(int score) =>
        Assert.False(EvaluationScorePolicy.IsValid(score));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void IsValid_WithinRange_ReturnsTrue(int score) =>
        Assert.True(EvaluationScorePolicy.IsValid(score));

    // ── ToMatchLevel ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EvaluationScorePolicy.StrongThreshold)]   // boundary: exactly 70
    [InlineData(85)]
    [InlineData(100)]
    public void ToMatchLevel_StrongRange_ReturnsStrong(int score) =>
        Assert.Equal(MatchLevel.Strong, EvaluationScorePolicy.ToMatchLevel(score));

    [Theory]
    [InlineData(EvaluationScorePolicy.MediumThreshold)]   // boundary: exactly 40
    [InlineData(55)]
    [InlineData(EvaluationScorePolicy.StrongThreshold - 1)] // boundary: 69
    public void ToMatchLevel_MediumRange_ReturnsMedium(int score) =>
        Assert.Equal(MatchLevel.Medium, EvaluationScorePolicy.ToMatchLevel(score));

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(EvaluationScorePolicy.MediumThreshold - 1)] // boundary: 39
    public void ToMatchLevel_WeakRange_ReturnsWeak(int score) =>
        Assert.Equal(MatchLevel.Weak, EvaluationScorePolicy.ToMatchLevel(score));

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ToMatchLevel_InvalidScore_Throws(int score) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EvaluationScorePolicy.ToMatchLevel(score));

    // Every valid score maps to some MatchLevel — no gaps or throws
    [Fact]
    public void ToMatchLevel_AllValidScores_NeverThrows()
    {
        for (var score = EvaluationScorePolicy.MinScore;
             score <= EvaluationScorePolicy.MaxScore;
             score++)
        {
            var ex = Record.Exception(() => EvaluationScorePolicy.ToMatchLevel(score));
            Assert.Null(ex);
        }
    }
}
