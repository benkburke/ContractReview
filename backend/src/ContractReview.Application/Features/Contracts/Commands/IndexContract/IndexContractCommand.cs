using ContractReview.Application.Abstractions.Messaging;

namespace ContractReview.Application.Features.Contracts.Commands.IndexContract;

/// <summary>Chunks and embeds a contract. Returns null when the contract does not exist.</summary>
/// <param name="Id">The contract to index.</param>
/// <param name="ResumeStale">Take over a contract left Indexing by an interrupted run (startup backfill only).</param>
public sealed record IndexContractCommand(Guid Id, bool ResumeStale = false) : ICommand<ContractDetailDto?>;
