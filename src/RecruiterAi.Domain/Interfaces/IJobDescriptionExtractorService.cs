namespace RecruiterAi.Domain.Interfaces;

public interface IJobDescriptionExtractorService
{
    Task<JobDescriptionExtractionResult> ExtractAsync(
        string jobDescriptionText,
        CancellationToken cancellationToken = default);
}

// ── Result types ──────────────────────────────────────────────────────────────

public record JobDescriptionExtractionResult(
    string Title,
    string Description,
    string? Country,
    string? SeniorityLevel,
    List<ExtractedSkill> RequiredSkills,
    List<ExtractedSkill> NiceToHaveSkills,
    ExtractionConfidence Confidence,
    List<string> MissingInformation,
    ExtractionMetadata Metadata);

// evidence is a verbatim quote from the source text that justifies the skill —
// makes hallucinations visible: if evidence doesn't match input, extraction is wrong.
public record ExtractedSkill(string Name, string Evidence);

public record ExtractionConfidence(
    DetectionConfidence Country,
    DetectionConfidence Seniority,
    DetectionConfidence Skills);

public enum DetectionConfidence
{
    High,        // explicitly stated in the text
    Low,         // inferred indirectly
    NotDetected, // absent from the text
}

public record ExtractionMetadata(
    string Model,
    string PromptVersion,
    DateTimeOffset ExtractedAt,
    int InputCharCount);
