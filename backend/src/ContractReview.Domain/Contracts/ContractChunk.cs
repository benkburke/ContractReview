namespace ContractReview.Domain.Contracts;

/// <summary>A section-tagged piece of a contract's text with its embedding (float32, little-endian).</summary>
public sealed class ContractChunk
{
    private ContractChunk()
    {
    }

    public Guid Id { get; private set; }

    public Guid ContractId { get; private set; }

    public int SectionOrder { get; private set; }

    public string SectionHeading { get; private set; } = string.Empty;

    public int ChunkOrder { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public byte[] Embedding { get; private set; } = [];

    internal static ContractChunk Create(Guid contractId, ContractChunkData data) => new()
    {
        Id = Guid.NewGuid(),
        ContractId = contractId,
        SectionOrder = data.SectionOrder,
        SectionHeading = data.SectionHeading,
        ChunkOrder = data.ChunkOrder,
        Text = data.Text,
        Embedding = EmbeddingVector.ToBytes(data.Vector),
    };
}
