namespace ContractReview.Application.Features.Claims;

public sealed record EvaluateClaimRequest(string ClaimText, string? CoverageLevel = null);
