namespace ContractReview.Domain.Errors;

public static class ErrorMessages
{
    public const string DomainRuleViolated = "A business rule was violated.";
    public const string Conflict = "The request conflicts with the current state.";
    public const string ContractFileNotPdf = "The file must be a non-empty PDF with a name ending in .pdf.";
    public const string ContractFileTooLarge = "The file is larger than the allowed upload size.";
    public const string ContractTitleInvalid = "The title is required and must be at most 200 characters.";
    public const string ContractNoText = "The PDF contains no extractable text. Scanned or image-only PDFs are not supported.";
    public const string ContractPdfUnreadable = "The PDF could not be read. It may be corrupt or encrypted.";
    public const string ContractNotFound = "The contract was not found.";
    public const string ContractIndexingInProgress = "The contract is already being indexed.";
    public const string ContractEmbeddingFailed = "The contract could not be indexed because the embedding service failed.";
    public const string ContractTooManyChunks = "The contract has too many chunks to index.";
}
