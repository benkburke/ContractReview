namespace ContractReview.Domain.Contracts;

public static class ContractRules
{
    public const int MaxTitleLength = 200;
    public const int MaxFileNameLength = 255;
    public const int MaxContentTypeLength = 100;
    public const string PdfExtension = ".pdf";
    public const string PdfContentType = "application/pdf";
    public const char ContentTypeParameterSeparator = ';';
    public const string PageSeparator = "\n\n";

    /// <summary>The bytes every PDF file starts with: "%PDF-".</summary>
    public static ReadOnlySpan<byte> PdfHeader => "%PDF-"u8;
}
