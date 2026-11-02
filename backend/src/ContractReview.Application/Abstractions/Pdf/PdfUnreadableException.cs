namespace ContractReview.Application.Abstractions.Pdf;

/// <summary>Raised by <see cref="IPdfTextExtractor"/> when a PDF is corrupt, encrypted or otherwise cannot be read.</summary>
public sealed class PdfUnreadableException(Exception? innerException = null) : Exception(null, innerException);
