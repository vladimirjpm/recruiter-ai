namespace RecruiterAi.Domain.Interfaces;

public interface ICvParserService
{
    Task<string> ParseAsync(Stream pdfStream, CancellationToken ct = default);
}
