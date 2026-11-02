namespace ContractReview.Application.Features.Contracts;

public sealed record ContractSummaryDto(
    Guid Id,
    string Title,
    string FileName,
    long SizeBytes,
    int PageCount,
    DateTime UploadedAtUtc);
