namespace ContractReview.Application.Features.Contracts.Indexing;

/// <summary>A chunk produced by the chunker, before it is embedded.</summary>
public sealed record ContractChunkDraft(int SectionOrder, string SectionHeading, int ChunkOrder, string Text);
