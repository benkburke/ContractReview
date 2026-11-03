using ContractReview.Application.Abstractions.Chat;
using ContractReview.Application.Options;
using ContractReview.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractReview.Infrastructure.Chat;

/// <summary>
/// Calls OpenRouter's OpenAI-compatible <c>POST {BaseUrl}/chat/completions</c> with temperature 0 and a strict JSON
/// schema response format (D-016, D-031). Each HTTP attempt has the <see cref="ClaimsOptions.ChatTimeoutSeconds"/>
/// timeout; 429, 5xx, timeouts and transport errors are retried <see cref="OpenRouterOptions.MaxRetries"/> times like
/// the embedding client. Output that does not match the schema or the limits is retried once, then fails.
/// </summary>
public sealed class OpenRouterChatClient(
    HttpClient http,
    IOptions<OpenRouterOptions> options,
    TimeProvider clock,
    ILogger<OpenRouterChatClient> logger) : IChatCompletionClient
{
    public string ModelName => options.Value.ChatModel;

    /// <summary>Applies base address and the chat timeout; used when registering the typed client.</summary>
    public static void Configure(HttpClient client, OpenRouterOptions settings, ClaimsOptions claims)
    {
        var baseUrl = settings.BaseUrl.TrimEnd(OpenRouterApi.PathSeparator) + OpenRouterApi.PathSeparator;
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(claims.ChatTimeoutSeconds);
    }

    public async Task<LlmClaimAnswer> EvaluateAsync(ClaimPrompt prompt, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning(
                "OpenRouter API key is not configured; set {ConfigKey} in user-secrets or an environment variable",
                $"{OpenRouterOptions.SectionName}:{nameof(OpenRouterOptions.ApiKey)}");
            throw new ChatFailedException(ChatFailureReasons.MissingApiKey);
        }

        var request = ChatWire.BuildRequest(settings.ChatModel, prompt);
        for (var attempt = 0; ; attempt++)
        {
            var content = await OpenRouterChatTransport.SendAsync(http, request, settings, clock, logger, cancellationToken);
            if (ChatWire.ParseAnswer(content, out var failureReason) is { } answer)
            {
                return answer;
            }

            logger.LogWarning(
                "OpenRouter chat returned malformed output (attempt {Attempt}): {Reason}", attempt + 1, failureReason);
            if (attempt >= ChatApi.MalformedOutputRetries)
            {
                throw new ChatFailedException(ChatFailureReasons.MalformedOutput);
            }
        }
    }
}
