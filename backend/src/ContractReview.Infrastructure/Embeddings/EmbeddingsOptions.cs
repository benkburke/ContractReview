namespace ContractReview.Infrastructure.Embeddings;

public enum EmbeddingProvider
{
    OpenRouter,
    Fake,
}

public sealed class EmbeddingsOptions
{
    public const string SectionName = "Embeddings";

    /// <summary><see cref="EmbeddingProvider.Fake"/> in tests, CI and e2e (D-028); never calls OpenRouter.</summary>
    public EmbeddingProvider Provider { get; init; } = EmbeddingProvider.OpenRouter;
}
