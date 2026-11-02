namespace ContractReview.Application.Abstractions.Pdf;

public interface IPdfTextExtractor
{
    /// <summary>Reads the page count and the text (page texts joined by blank lines).</summary>
    /// <exception cref="PdfUnreadableException">The file is corrupt or encrypted.</exception>
    PdfExtractionResult Extract(byte[] content);
}
