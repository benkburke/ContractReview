using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContractReview.Application.Abstractions.Chat;
using ContractReview.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging;

namespace ContractReview.Infrastructure.Chat;

internal static class OpenRouterChatTransport
{
    /// <summary>One logical chat completions request with transient-failure retries, shared by the evaluator and the reviewer; returns the message content, or null when absent or unreadable.</summary>
    public static async Task<string?> SendAsync(
        HttpClient http,
        ChatRequest request,
        OpenRouterOptions settings,
        TimeProvider clock,
        ILogger logger,
        CancellationToken cancellationToken)
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

                await DelayAsync(settings, clock, attempt, null, cancellationToken);
                continue;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("OpenRouter chat request timed out (attempt {Attempt})", attempt + 1);
                if (!canRetry)
                {
                    throw new ChatFailedException(ChatFailureReasons.Timeout, ex);
                }

                await DelayAsync(settings, clock, attempt, null, cancellationToken);
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

                await DelayAsync(settings, clock, attempt, response.Headers.RetryAfter, cancellationToken);
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

    private static Task DelayAsync(
        OpenRouterOptions settings, TimeProvider clock, int attempt, RetryConditionHeaderValue? retryAfter, CancellationToken cancellationToken)
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
