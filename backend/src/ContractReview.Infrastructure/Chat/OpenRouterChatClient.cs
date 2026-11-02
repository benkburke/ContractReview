using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
            var content = await SendAsync(request, settings, cancellationToken);
            if (ChatWire.ParseAnswer(content) is { } answer)
            {
                return answer;
            }

            logger.LogWarning("OpenRouter chat returned malformed output (attempt {Attempt})", attempt + 1);
            if (attempt >= ChatApi.MalformedOutputRetries)
            {
                throw new ChatFailedException(ChatFailureReasons.MalformedOutput);
            }
        }
    }

    /// <summary>One logical request with transient-failure retries; returns the message content, or null when absent or unreadable.</summary>
    private async Task<string?> SendAsync(ChatRequest request, OpenRouterOptions settings, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var canRetry = attempt < settings.MaxRetries;
            HttpResponseMessage response;
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, OpenRouterApi.ChatCompletionsPath)
                {
                    Content = JsonContent.Create(request, options: ChatWire.Json),
                };
                message.Headers.Authorization = new AuthenticationHeaderValue(OpenRouterApi.BearerScheme, settings.ApiKey);
                response = await http.SendAsync(message, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "OpenRouter chat request failed (attempt {Attempt})", attempt + 1);
                if (!canRetry)
                {
                    throw new ChatFailedException(ChatFailureReasons.TransportError, ex);
                }

                await DelayAsync(settings, attempt, null, cancellationToken);
                continue;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("OpenRouter chat request timed out (attempt {Attempt})", attempt + 1);
                if (!canRetry)
                {
                    throw new ChatFailedException(ChatFailureReasons.Timeout, ex);
                }

                await DelayAsync(settings, attempt, null, cancellationToken);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return await ReadContentAsync(response, cancellationToken);
                }

                var transient = response.StatusCode == HttpStatusCode.TooManyRequests
                    || response.StatusCode >= HttpStatusCode.InternalServerError;
                logger.LogWarning(
                    "OpenRouter chat returned {StatusCode} (attempt {Attempt})", (int)response.StatusCode, attempt + 1);
                if (!transient || !canRetry)
                {
                    throw new ChatFailedException(ChatFailureReasons.HttpError);
                }

                await DelayAsync(settings, attempt, response.Headers.RetryAfter, cancellationToken);
            }
        }
    }

    private static async Task<string?> ReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ChatResponse>(ChatWire.Json, cancellationToken);
            return body?.Choices?.FirstOrDefault()?.Message?.Content;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ChatFailedException(ChatFailureReasons.Timeout);
        }
    }

    private Task DelayAsync(
        OpenRouterOptions settings, int attempt, RetryConditionHeaderValue? retryAfter, CancellationToken cancellationToken)
    {
        var delay = retryAfter?.Delta
            ?? TimeSpan.FromMilliseconds(settings.RetryDelayMilliseconds * Math.Pow(2, attempt));
        if (delay > OpenRouterEmbeddingClient.MaxRetryAfter)
        {
            delay = OpenRouterEmbeddingClient.MaxRetryAfter;
        }

        return Task.Delay(delay, clock, cancellationToken);
    }
}
