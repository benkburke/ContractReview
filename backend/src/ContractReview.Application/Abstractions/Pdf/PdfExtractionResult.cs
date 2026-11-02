namespace ContractReview.Application.Abstractions.Pdf;

public sealed record PdfExtractionResult(int PageCount, string Text);
