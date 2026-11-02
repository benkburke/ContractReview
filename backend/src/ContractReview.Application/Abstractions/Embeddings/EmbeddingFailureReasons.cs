namespace ContractReview.Application.Abstractions.Embeddings;

/// <summary>Internal failure reasons for logs; clients only ever see <c>contract.embedding_failed</c>.</summary>
public static class EmbeddingFailureReasons
{
    public const string MissingApiKey = "The embedding API key is not configured.";
    public const string HttpError = "The embedding service returned an error status.";
    public const string Timeout = "The embedding service timed out.";
    public const string TransportError = "The embedding service could not be reached.";
    public const string InvalidResponse = "The embedding service returned an invalid response.";
    public const string CountMismatch = "The embedding service returned a different number of vectors than inputs.";
    public const string DimensionMismatch = "The embedding vectors are empty or have different dimensions.";
}
