namespace ContractReview.Application.Features.Contracts;

public sealed record ContractDetailDto(
    Guid Id,
    string Title,
    string FileName,
    long SizeBytes,
    int PageCount,
    DateTime UploadedAtUtc,
    string ExtractedText);
