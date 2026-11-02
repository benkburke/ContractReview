namespace ContractReview.Infrastructure.Embeddings;

/// <summary>Wire details of the OpenAI-compatible embeddings API served by OpenRouter.</summary>
public static class OpenRouterApi
{
    /// <summary>Relative to <see cref="OpenRouterOptions.BaseUrl"/>.</summary>
    public const string EmbeddingsPath = "embeddings";
    public const string ChatCompletionsPath = "chat/completions";
    public const string BearerScheme = "Bearer";
    public const string EncodingFormatFloat = "float";
    public const char PathSeparator = '/';
}

internal sealed record EmbeddingRequest(string Model, IReadOnlyList<string> Input, string EncodingFormat);

internal sealed record EmbeddingResponse(IReadOnlyList<EmbeddingData>? Data);

internal sealed record EmbeddingData(int Index, float[]? Embedding);
