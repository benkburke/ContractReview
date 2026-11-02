using ContractReview.Domain.Claims;

namespace ContractReview.Application.Features.Claims;

public sealed record ClaimEvaluationDto(
    Guid Id,
    Guid ContractId,
    string ClaimText,
    string? CoverageLevelRequested,
    ClaimState State,
    DateTime CreatedAtUtc,
    ClaimEvaluationResultDto? Result,
    string? ErrorCode);
