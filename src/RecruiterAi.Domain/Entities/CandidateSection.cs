namespace RecruiterAi.Domain.Entities;

/// <summary>
/// Resume section used for RAG-style retrieval (experience / skills / education / summary).
/// Phase 1: table is created but not populated.
/// Phase 2: populated by IEmbeddingService through text-embedding-3-small.
///
/// Embedding is stored as vector(1536) in pgvector at the database level, but
/// modelled as float[] at the domain level to keep the Domain project free of
/// pgvector dependencies. The pgvector mapping is wired in Phase 2 via
/// Pgvector.EntityFrameworkCore.
/// </summary>
public class CandidateSection
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;

    /// <summary>
    /// Section type: "experience" / "skills" / "education" / "summary".
    /// </summary>
    public string SectionType { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Section embedding. NULL in Phase 1.
    /// In the database: vector(1536) (pgvector). In .NET: float[] for domain purity.
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Embedding model name, e.g. "text-embedding-3-small".
    /// </summary>
    public string? EmbeddingModel { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
