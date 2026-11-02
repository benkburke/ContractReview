using ContractReview.Domain.Errors;

namespace ContractReview.Domain.Contracts;

public sealed class Contract
{
    private Contract()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public int PageCount { get; private set; }

    public byte[] FileContent { get; private set; } = [];

    public string ExtractedText { get; private set; } = string.Empty;

    public DateTime UploadedAtUtc { get; private set; }

    public static Contract Upload(
        string? title,
        string? fileName,
        string contentType,
        byte[] content,
        string? extractedText,
        int pageCount,
        long maxFileBytes,
        DateTime uploadedAtUtc)
    {
        var normalizedTitle = NormalizeTitle(title);
        EnsureFile(fileName, content.LongLength, maxFileBytes);

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new DomainException(ErrorCodes.ContractNoText, ErrorMessages.ContractNoText);
        }

        if (pageCount < 1)
        {
            throw new DomainException(ErrorCodes.ContractPdfUnreadable, ErrorMessages.ContractPdfUnreadable);
        }

        return new Contract
        {
            Id = Guid.NewGuid(),
            Title = normalizedTitle,
            FileName = fileName!,
            ContentType = contentType,
            SizeBytes = content.LongLength,
            PageCount = pageCount,
            FileContent = content,
            ExtractedText = extractedText,
            UploadedAtUtc = uploadedAtUtc,
        };
    }

    /// <summary>Trims the title and checks it is 1 to <see cref="ContractRules.MaxTitleLength"/> characters.</summary>
    public static string NormalizeTitle(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > ContractRules.MaxTitleLength)
        {
            throw new DomainException(ErrorCodes.ContractTitleInvalid, ErrorMessages.ContractTitleInvalid);
        }

        return trimmed;
    }

    /// <summary>Checks the file name ends in .pdf (1 to 255 characters) and the size is within 1 byte to the limit.</summary>
    public static void EnsureFile(string? fileName, long sizeBytes, long maxFileBytes)
    {
        if (string.IsNullOrEmpty(fileName)
            || fileName.Length > ContractRules.MaxFileNameLength
            || !fileName.EndsWith(ContractRules.PdfExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(ErrorCodes.ContractFileNotPdf, ErrorMessages.ContractFileNotPdf);
        }

        if (sizeBytes < 1)
        {
            throw new DomainException(ErrorCodes.ContractFileNotPdf, ErrorMessages.ContractFileNotPdf);
        }

        if (sizeBytes > maxFileBytes)
        {
            throw new DomainException(ErrorCodes.ContractFileTooLarge, ErrorMessages.ContractFileTooLarge);
        }
    }
}
