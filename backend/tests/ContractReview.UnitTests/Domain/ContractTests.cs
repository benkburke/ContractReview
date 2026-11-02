using ContractReview.Domain.Contracts;
using ContractReview.Domain.Errors;

namespace ContractReview.UnitTests.Domain;

public class ContractTests
{
    private const long MaxBytes = 1024;
    private static readonly byte[] Content = "%PDF-1.7 body"u8.ToArray();
    private static readonly DateTime Now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static Contract Upload(
        string? title = "Lease",
        string? fileName = "lease.pdf",
        byte[]? content = null,
        string? text = "Some text",
        int pageCount = 1,
        long maxBytes = MaxBytes) =>
        Contract.Upload(title, fileName, ContractRules.PdfContentType, content ?? Content, text, pageCount, maxBytes, Now);

    [Fact]
    public void Upload_with_valid_data_trims_title_and_sets_size()
    {
        var contract = Upload(title: "  Lease  ", fileName: "LEASE.PDF");

        Assert.Equal("Lease", contract.Title);
        Assert.Equal(Content.Length, contract.SizeBytes);
        Assert.Equal(Now, contract.UploadedAtUtc);
        Assert.NotEqual(Guid.Empty, contract.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Upload_rejects_blank_title(string? title) =>
        AssertCode(ErrorCodes.ContractTitleInvalid, () => Upload(title: title));

    [Fact]
    public void Upload_rejects_title_over_200_characters() =>
        AssertCode(ErrorCodes.ContractTitleInvalid, () => Upload(title: new string('a', ContractRules.MaxTitleLength + 1)));

    [Fact]
    public void Upload_accepts_title_of_exactly_200_characters() =>
        Assert.Equal(ContractRules.MaxTitleLength, Upload(title: new string('a', ContractRules.MaxTitleLength)).Title.Length);

    [Theory]
    [InlineData("lease.docx")]
    [InlineData("")]
    [InlineData(null)]
    public void Upload_rejects_non_pdf_file_name(string? fileName) =>
        AssertCode(ErrorCodes.ContractFileNotPdf, () => Upload(fileName: fileName));

    [Fact]
    public void Upload_rejects_file_name_over_255_characters() =>
        AssertCode(ErrorCodes.ContractFileNotPdf, () => Upload(fileName: new string('a', ContractRules.MaxFileNameLength) + ".pdf"));

    [Fact]
    public void Upload_rejects_empty_content() =>
        AssertCode(ErrorCodes.ContractFileNotPdf, () => Upload(content: []));

    [Fact]
    public void Upload_rejects_content_over_the_limit() =>
        AssertCode(ErrorCodes.ContractFileTooLarge, () => Upload(maxBytes: Content.Length - 1));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n\n ")]
    public void Upload_rejects_empty_extracted_text(string? text) =>
        AssertCode(ErrorCodes.ContractNoText, () => Upload(text: text));

    [Fact]
    public void Upload_rejects_page_count_below_one() =>
        AssertCode(ErrorCodes.ContractPdfUnreadable, () => Upload(pageCount: 0));

    private static void AssertCode(string code, Action act) =>
        Assert.Equal(code, Assert.Throws<DomainException>(act).Code);
}
