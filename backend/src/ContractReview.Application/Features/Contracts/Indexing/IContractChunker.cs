namespace ContractReview.Application.Features.Contracts.Indexing;

/// <summary>Splits contract text into section-tagged chunks. Pure: no I/O.</summary>
public interface IContractChunker
{
    IReadOnlyList<ContractChunkDraft> Split(string text);
}
