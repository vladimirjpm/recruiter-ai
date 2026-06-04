namespace RecruiterAi.Domain.Interfaces;

/// <summary>
/// Embedding generation service.
/// Phase 1: interface only, no implementation.
/// Phase 2: implemented via OpenAI text-embedding-3-small (1536 dim).
///
/// The interface is declared upfront so Phase 2 does not require refactoring
/// existing code: drop in an implementation, register it in DI, and the rest
/// of the system picks it up automatically.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Produce embeddings for a batch of texts.
    /// Vector dimensionality depends on the model (1536 for text-embedding-3-small).
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
