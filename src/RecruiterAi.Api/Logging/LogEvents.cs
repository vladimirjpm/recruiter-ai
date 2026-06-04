namespace RecruiterAi.Api.Logging;

/// <summary>
/// Centralized EventId definitions for structured logging.
///
/// Usage: _logger.LogInformation(LogEvents.CvUploaded, "...", ...);
///
/// Categories:
///   1xxx — Positions
///   2xxx — Candidates / Upload / PDF parsing
///   3xxx — OpenAI / Evaluation
///   4xxx — CV Generator
/// </summary>
public static class LogEvents
{
    // Positions
    public static readonly Microsoft.Extensions.Logging.EventId PositionCreated = new(1001, nameof(PositionCreated));
    public static readonly Microsoft.Extensions.Logging.EventId PositionUpdated = new(1002, nameof(PositionUpdated));
    public static readonly Microsoft.Extensions.Logging.EventId PositionDeleted = new(1003, nameof(PositionDeleted));

    // Candidates / Upload (Stage 3)
    public static readonly Microsoft.Extensions.Logging.EventId CvUploadStarted = new(2001, nameof(CvUploadStarted));
    public static readonly Microsoft.Extensions.Logging.EventId CvUploadCompleted = new(2002, nameof(CvUploadCompleted));
    public static readonly Microsoft.Extensions.Logging.EventId CvUploadRejected = new(2003, nameof(CvUploadRejected));
    public static readonly Microsoft.Extensions.Logging.EventId PdfParseSuccess = new(2010, nameof(PdfParseSuccess));
    public static readonly Microsoft.Extensions.Logging.EventId PdfParseFailure = new(2011, nameof(PdfParseFailure));

    // OpenAI / Evaluation (Stage 4)
    public static readonly Microsoft.Extensions.Logging.EventId OpenAiRequestStarted = new(3001, nameof(OpenAiRequestStarted));
    public static readonly Microsoft.Extensions.Logging.EventId OpenAiRequestCompleted = new(3002, nameof(OpenAiRequestCompleted));
    public static readonly Microsoft.Extensions.Logging.EventId OpenAiRequestFailed = new(3003, nameof(OpenAiRequestFailed));
    public static readonly Microsoft.Extensions.Logging.EventId EvaluationCompleted = new(3010, nameof(EvaluationCompleted));
    public static readonly Microsoft.Extensions.Logging.EventId EvaluationFailed = new(3011, nameof(EvaluationFailed));

    // CV Generator (Stage 6)
    public static readonly Microsoft.Extensions.Logging.EventId GenerationBatchStarted = new(4001, nameof(GenerationBatchStarted));
    public static readonly Microsoft.Extensions.Logging.EventId GenerationBatchCompleted = new(4002, nameof(GenerationBatchCompleted));
    public static readonly Microsoft.Extensions.Logging.EventId GenerationBatchFailed = new(4003, nameof(GenerationBatchFailed));
}
