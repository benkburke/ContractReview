using ContractReview.Application.Abstractions.Messaging;

namespace ContractReview.Application.Features.Contracts.Queries.ListContracts;

public sealed record ListContractsQuery : IQuery<IReadOnlyList<ContractSummaryDto>>;
