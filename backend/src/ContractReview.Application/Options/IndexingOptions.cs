using System.ComponentModel.DataAnnotations;

namespace ContractReview.Application.Options;

public sealed class IndexingOptions : IValidatableObject
{
    public const string SectionName = "Indexing";
    public const int DefaultChunkSize = 1200;
    public const int DefaultChunkOverlap = 150;
    public const int DefaultMaxChunks = 2000;
    public const int DefaultEmbeddingBatchSize = 64;
    public const string ChunkOverlapInvalidMessage = "Indexing:ChunkOverlap must be between 0 and half of Indexing:ChunkSize.";

    [Range(200, 8000)]
    public int ChunkSize { get; init; } = DefaultChunkSize;

    [Range(0, int.MaxValue)]
    public int ChunkOverlap { get; init; } = DefaultChunkOverlap;

    [Range(1, 10000)]
    public int MaxChunks { get; init; } = DefaultMaxChunks;

    [Range(1, 256)]
    public int EmbeddingBatchSize { get; init; } = DefaultEmbeddingBatchSize;

    /// <summary>Index NotIndexed, Failed and stale Indexing contracts once when the API starts.</summary>
    public bool BackfillOnStartup { get; init; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ChunkOverlap > ChunkSize / 2)
        {
            yield return new ValidationResult(ChunkOverlapInvalidMessage, [nameof(ChunkOverlap)]);
        }
    }
}
