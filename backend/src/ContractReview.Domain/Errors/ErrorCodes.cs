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
}
