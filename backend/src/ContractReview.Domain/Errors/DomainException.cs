namespace ContractReview.Domain.Errors;

public class DomainException(string code, string message, DomainErrorKind kind = DomainErrorKind.Validation)
    : Exception(message)
{
    public string Code { get; } = code;

    public DomainErrorKind Kind { get; } = kind;
}
