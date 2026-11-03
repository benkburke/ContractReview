using ContractReview.Application.Abstractions.Messaging;

namespace ContractReview.Application.Features.Claims.Commands.EvaluateClaim;

public sealed record EvaluateClaimCommand(Guid ContractId, string ClaimText, string? CoverageLevel, string? ProgressConnectionId = null)
    : ICommand<ClaimEvaluationDto?>;
