namespace ContractReview.Application.Abstractions.Embeddings;

/// <summary>The embedding service failed, timed out, is not configured, or returned an invalid response.</summary>
public sealed class EmbeddingFailedException(string reason, Exception? innerException = null)
    : Exception(reason, innerException)
{
    public string Reason { get; } = reason;
}
