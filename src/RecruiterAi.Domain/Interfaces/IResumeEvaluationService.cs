using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Domain.Interfaces;

/// <summary>
/// Service that evaluates a candidate's resume against a position.
/// Phase 1: direct GPT-4o mini call with the full raw_text.
/// Phase 2: same interface, but with an upfront RAG step that selects only
/// the most relevant sections before invoking the LLM.
///
/// DI registration:
///   builder.Services.AddScoped&lt;IResumeEvaluationService, OpenAiResumeEvaluationService&gt;()
/// </summary>
public interface IResumeEvaluationService
{
    /// <summary>
    /// Evaluate a candidate against a position.
    /// Returns a populated Evaluation entity. The caller is responsible for
    /// persisting it.
    /// </summary>
    Task<Evaluation> EvaluateAsync(
        Candidate candidate,
        Position position,
        CancellationToken cancellationToken = default);
}
