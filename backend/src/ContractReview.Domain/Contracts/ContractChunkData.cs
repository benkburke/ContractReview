namespace ContractReview.Domain.Contracts;

/// <summary>A chunk to store on a contract: its place in the document, its text and its embedding vector.</summary>
public sealed record ContractChunkData(int SectionOrder, string SectionHeading, int ChunkOrder, string Text, float[] Vector);
