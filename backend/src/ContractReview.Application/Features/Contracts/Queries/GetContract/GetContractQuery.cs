using ContractReview.Application.Abstractions.Messaging;

namespace ContractReview.Application.Features.Contracts.Queries.GetContract;

public sealed record GetContractQuery(Guid Id) : IQuery<ContractDetailDto?>;
