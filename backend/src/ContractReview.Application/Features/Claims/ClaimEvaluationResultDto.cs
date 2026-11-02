using ContractReview.Domain.Claims;

namespace ContractReview.Application.Features.Claims;

public sealed record ClaimEvaluationResultDto(
    ClaimStatus Status,
    int Confidence,
    string? CoverageLevel,
    bool NeedsCoverageLevel,
    IReadOnlyList<string> CoverageLevelOptions,
    IReadOnlyList<string> CoveredItems,
    IReadOnlyList<string> ExcludedItems,
    string Reasoning,
    string? FollowUpQuestion,
    IReadOnlyList<ClaimCitationDto> Citations,
    string Model);
