using ContractReview.Domain.Claims;

namespace ContractReview.Application.Features.Claims.Queries.ListClaims;

public sealed record ClaimSummaryDto(
    Guid Id,
    string ClaimText,
    ClaimState State,
    ClaimStatus? Status,
    int? Confidence,
    bool? ReviewAgrees,
    DateTime CreatedAtUtc);

public sealed record ClaimHistoryPageDto(IReadOnlyList<ClaimSummaryDto> Items, int Page, int PageSize, int TotalCount);
