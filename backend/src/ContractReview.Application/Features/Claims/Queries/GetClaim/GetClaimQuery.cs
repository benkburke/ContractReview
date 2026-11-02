using ContractReview.Application.Abstractions.Messaging;

namespace ContractReview.Application.Features.Claims.Queries.GetClaim;

public sealed record GetClaimQuery(Guid ContractId, Guid ClaimId) : IQuery<ClaimEvaluationDto>;
