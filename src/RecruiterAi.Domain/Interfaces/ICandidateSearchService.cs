using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Domain.Interfaces;

/// <summary>
/// Candidate search service.
/// Phase 1: PostgreSQL filters + text search over raw_text.
/// Phase 2: pgvector cosine similarity over candidate_sections.embedding.
///
/// Controllers depend only on this interface, so swapping the Phase 2
/// implementation does not require changes in the API layer.
/// </summary>
public interface ICandidateSearchService
{
    Task<IReadOnlyList<Candidate>> SearchAsync(
        Guid? positionId,
        string? query,
        int take,
        CancellationToken cancellationToken = default);
}
