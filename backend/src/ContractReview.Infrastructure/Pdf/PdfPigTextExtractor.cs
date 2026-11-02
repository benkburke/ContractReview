using ContractReview.Application.Abstractions.Pdf;
using ContractReview.Domain.Contracts;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ContractReview.Infrastructure.Pdf;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public PdfExtractionResult Extract(byte[] content)
    {
        try
        {
            using var document = PdfDocument.Open(content);
            var pageTexts = document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)).ToList();
            return new PdfExtractionResult(pageTexts.Count, string.Join(ContractRules.PageSeparator, pageTexts));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PdfPig throws many exception types for corrupt or encrypted files; all mean "unreadable" here.
            throw new PdfUnreadableException(ex);
        }
    }
}
