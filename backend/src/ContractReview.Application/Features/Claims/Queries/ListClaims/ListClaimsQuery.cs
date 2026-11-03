using ContractReview.Application.Abstractions.Messaging;

namespace ContractReview.Application.Features.Claims.Queries.ListClaims;

public sealed record ListClaimsQuery(Guid ContractId, int Page, int PageSize) : IQuery<ClaimHistoryPageDto>;
