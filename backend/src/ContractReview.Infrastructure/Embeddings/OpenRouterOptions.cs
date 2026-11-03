using System.ComponentModel.DataAnnotations;
using ContractReview.Domain.Claims;
using ContractReview.Domain.Contracts;

namespace ContractReview.Infrastructure.Embeddings;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";
    public const string DefaultBaseUrl = "https://openrouter.ai/api/v1";
    public const string DefaultEmbeddingModel = "openai/text-embedding-3-small";
    public const string DefaultChatModel = "openai/gpt-4o-mini";
    public const string DefaultReviewModel = "anthropic/claude-haiku-4.5";
    public const int DefaultTimeoutSeconds = 30;
    public const int DefaultMaxRetries = 2;
    public const int DefaultRetryDelayMilliseconds = 1000;

    [Required]
    [Url]
    public string BaseUrl { get; init; } = DefaultBaseUrl;

    [Required]
    [MaxLength(ContractRules.MaxEmbeddingModelLength)]
    public string EmbeddingModel { get; init; } = DefaultEmbeddingModel;

    /// <summary>The chat model that evaluates claims (D-031).</summary>
    [Required]
    [MaxLength(ClaimRules.MaxModelLength)]
    public string ChatModel { get; init; } = DefaultChatModel;

    /// <summary>The model that reviews the primary answer; a different model for independent verification (D-042).</summary>
    [Required]
    [MaxLength(ClaimRules.MaxModelLength)]
    public string ReviewModel { get; init; } = DefaultReviewModel;

    /// <summary>From user-secrets or the <c>OpenRouter__ApiKey</c> env var only. Missing: indexing fails, the app still starts.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Timeout per HTTP call (each attempt).</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    /// <summary>Retries after a 429, 5xx, timeout or transport error.</summary>
    [Range(0, 5)]
    public int MaxRetries { get; init; } = DefaultMaxRetries;

    /// <summary>Base delay before the first retry; doubles per retry unless the response sends Retry-After.</summary>
    [Range(0, 60000)]
    public int RetryDelayMilliseconds { get; init; } = DefaultRetryDelayMilliseconds;
}
