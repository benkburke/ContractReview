namespace ContractReview.Domain.Errors;

public static class ErrorCodes
{
    public const string DomainRuleViolated = "domain.rule_violated";
    public const string Conflict = "domain.conflict";
    public const string ContractFileNotPdf = "contract.file_not_pdf";
    public const string ContractFileTooLarge = "contract.file_too_large";
    public const string ContractTitleInvalid = "contract.title_invalid";
    public const string ContractNoText = "contract.no_text";
    public const string ContractPdfUnreadable = "contract.pdf_unreadable";
    public const string ContractNotFound = "contract.not_found";
    public const string ContractIndexingInProgress = "contract.indexing_in_progress";
    public const string ContractEmbeddingFailed = "contract.embedding_failed";
    public const string ContractTooManyChunks = "contract.too_many_chunks";
    public const string ContractNotIndexed = "contract.not_indexed";
    public const string ContractEmbeddingModelMismatch = "contract.embedding_model_mismatch";
    public const string ClaimTextInvalid = "claim.text_invalid";
    public const string ClaimCoverageLevelInvalid = "claim.coverage_level_invalid";
    public const string ClaimNotFound = "claim.not_found";
    public const string ClaimEvaluationFailed = "claim.evaluation_failed";
    public const string ReviewFailed = "review.failed";
}
