using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Domain.Interfaces;

/// <summary>
/// Generator of synthetic CVs used to validate the evaluator.
/// Domain-agnostic: works for any profession (software engineer, bus driver,
/// electrician, nurse, cleaner, ...). Requirements are inferred by the LLM
/// from the job description — never hardcoded for a specific domain.
/// </summary>
public interface ICvGenerationService
{
    /// <summary>
    /// Generate N synthetic CVs with varying fit levels against the position.
    /// Returned candidates have Source=Generated and a populated ExpectedFitLevel.
    /// </summary>
    Task<CvGenerationResult> GenerateAsync(
        Position position,
        int count,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a generation request: the batch metadata plus the synthetic candidates.
/// The caller persists both within a single transaction.
/// </summary>
public record CvGenerationResult(
    CvGenerationBatch Batch,
    IReadOnlyList<Candidate> Candidates);
