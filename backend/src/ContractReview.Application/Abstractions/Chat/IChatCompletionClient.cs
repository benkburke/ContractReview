namespace ContractReview.Application.Abstractions.Chat;

/// <summary>Asks a chat model to evaluate a claim against retrieved contract chunks.</summary>
public interface IChatCompletionClient
{
    /// <summary>The model name stored with the result.</summary>
    string ModelName { get; }

    /// <summary>
    /// Returns the model's validated answer. Throws <see cref="ChatFailedException"/> on any failure: not configured,
    /// HTTP error, timeout, or output that is still malformed after the one retry.
    /// </summary>
    Task<LlmClaimAnswer> EvaluateAsync(ClaimPrompt prompt, CancellationToken cancellationToken);
}
