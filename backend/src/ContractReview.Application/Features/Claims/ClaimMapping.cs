using ContractReview.Domain.Claims;

namespace ContractReview.Application.Features.Claims;

public static class ClaimMapping
{
    public static ClaimEvaluationResultDto ToDto(ClaimEvaluationResult result) => new(
        result.Status,
        result.Confidence,
        result.CoverageLevel,
        result.NeedsCoverageLevel,
        result.CoverageLevelOptions,
        result.CoveredItems,
        result.ExcludedItems,
        result.Reasoning,
        result.FollowUpQuestion,
        result.Citations.Select(c => new ClaimCitationDto(c.SectionHeading, c.Excerpt)).ToList(),
        result.Model);

    /// <summary>Maps a finished claim (Evaluated or Failed) to its DTO.</summary>
    public static ClaimEvaluationDto ToDto(Claim claim) => new(
        claim.Id,
        claim.ContractId,
        claim.ClaimText,
        claim.CoverageLevelRequested,
        claim.State ?? ClaimState.Failed,
        claim.CreatedAtUtc,
        claim.Result is { } result ? ToDto(result) : null,
        claim.ErrorCode);
}
