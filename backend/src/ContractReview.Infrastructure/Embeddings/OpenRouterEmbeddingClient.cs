using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContractReview.Application.Abstractions.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractReview.Infrastructure.Embeddings;

/// <summary>
/// Calls OpenRouter's OpenAI-compatible <c>POST {BaseUrl}/embeddings</c> (D-027). Each attempt has the configured
/// timeout; 429, 5xx, timeouts and transport errors are retried <see cref="OpenRouterOptions.MaxRetries"/> times.
/// </summary>
public sealed class OpenRouterEmbeddingClient(
    HttpClient http,
    IOptions<OpenRouterOptions> options,
    TimeProvider clock,
    ILogger<OpenRouterEmbeddingClient> logger) : IEmbeddingClient
{
    /// <summary>Upper bound for a server-sent Retry-After, so one call cannot stall indexing for long.</summary>
    public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public string ModelName => options.Value.EmbeddingModel;

    /// <summary>Applies base address and timeout; used when registering the typed client.</summary>
    public static void Configure(HttpClient client, OpenRouterOptions settings)
    {
        var baseUrl = settings.BaseUrl.TrimEnd(OpenRouterApi.PathSeparator) + OpenRouterApi.PathSeparator;
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning(
                "OpenRouter API key is not configured; set {ConfigKey} in user-secrets or an environment variable",
                $"{OpenRouterOptions.SectionName}:{nameof(OpenRouterOptions.ApiKey)}");
            throw new EmbeddingFailedException(EmbeddingFailureReasons.MissingApiKey);
        }

        var request = new EmbeddingRequest(settings.EmbeddingModel, inputs, OpenRouterApi.EncodingFormatFloat);
        for (var attempt = 0; ; attempt++)
        {
            var canRetry = attempt < settings.MaxRetries;
            HttpResponseMessage response;
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, OpenRouterApi.EmbeddingsPath)
                {
                    Content = JsonContent.Create(request, options: Json),
                };
                message.Headers.Authorization = new AuthenticationHeaderValue(OpenRouterApi.BearerScheme, settings.ApiKey);
                response = await http.SendAsync(message, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "OpenRouter embeddings request failed (attempt {Attempt})", attempt + 1);
                if (!canRetry)
                {
                    throw new EmbeddingFailedException(EmbeddingFailureReasons.TransportError, ex);
                }

                await DelayAsync(settings, attempt, null, cancellationToken);
                continue;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("OpenRouter embeddings request timed out (attempt {Attempt})", attempt + 1);
                if (!canRetry)
                {
                    throw new EmbeddingFailedException(EmbeddingFailureReasons.Timeout, ex);
                }

                await DelayAsync(settings, attempt, null, cancellationToken);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return await ReadVectorsAsync(response, inputs.Count, cancellationToken);
                }

                var transient = response.StatusCode == HttpStatusCode.TooManyRequests
                    || response.StatusCode >= HttpStatusCode.InternalServerError;
                logger.LogWarning(
                    "OpenRouter embeddings returned {StatusCode} (attempt {Attempt})", (int)response.StatusCode, attempt + 1);
                if (!transient || !canRetry)
                {
                    throw new EmbeddingFailedException(EmbeddingFailureReasons.HttpError);
                }

                await DelayAsync(settings, attempt, response.Headers.RetryAfter, cancellationToken);
            }
        }
    }

    private static async Task<IReadOnlyList<float[]>> ReadVectorsAsync(
        HttpResponseMessage response, int expectedCount, CancellationToken cancellationToken)
    {
        EmbeddingResponse? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(Json, cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new EmbeddingFailedException(EmbeddingFailureReasons.InvalidResponse, ex);
        }

        if (body?.Data is not { } data)
        {
            throw new EmbeddingFailedException(EmbeddingFailureReasons.InvalidResponse);
        }

        if (data.Count != expectedCount)
        {
            throw new EmbeddingFailedException(EmbeddingFailureReasons.CountMismatch);
        }

        var vectors = data.OrderBy(d => d.Index).Select(d => d.Embedding).ToList();
        var dimensions = vectors[0]?.Length ?? 0;
        if (dimensions == 0 || vectors.Any(v => v is null || v.Length != dimensions))
        {
            throw new EmbeddingFailedException(EmbeddingFailureReasons.DimensionMismatch);
        }

        return vectors!;
    }

    private Task DelayAsync(
        OpenRouterOptions settings, int attempt, RetryConditionHeaderValue? retryAfter, CancellationToken cancellationToken)
    {
        var delay = retryAfter?.Delta
            ?? TimeSpan.FromMilliseconds(settings.RetryDelayMilliseconds * Math.Pow(2, attempt));
        if (delay > MaxRetryAfter)
        {
            delay = MaxRetryAfter;
        }

        return Task.Delay(delay, clock, cancellationToken);
    }
}
