using System.Text;
using RecruiterAi.Domain.Interfaces;
using UglyToad.PdfPig;

namespace RecruiterAi.Infrastructure.Pdf;

// PdfPig has no async API; we wrap synchronous parsing in Task.FromResult
// to match the async interface contract without introducing Thread.Run overhead
// for files that are already buffered in memory before this call.
public sealed class PdfPigCvParser : ICvParserService
{
    public Task<string> ParseAsync(Stream pdfStream, CancellationToken ct = default)
    {
        using var document = PdfDocument.Open(pdfStream);
        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            sb.AppendLine(page.Text);
        }
        return Task.FromResult(sb.ToString());
    }
}
