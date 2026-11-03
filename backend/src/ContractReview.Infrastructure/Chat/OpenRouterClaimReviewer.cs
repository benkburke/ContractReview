using ContractReview.Application.Abstractions.Chat;
using ContractReview.Application.Features.Claims.Evaluation;
using ContractReview.Application.Options;
using ContractReview.Domain.Claims;
using ContractReview.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractReview.Infrastructure.Chat;

/// <summary>
/// Reviews the primary answer through OpenRouter's chat completions with <see cref="OpenRouterOptions.ReviewModel"/>,
/// temperature 0 and a strict JSON schema. The whole review, including transient retries, is limited by
/// <see cref="ReviewOptions.TimeoutSeconds"/>; invalid output is retried once, then fails.
/// </summary>
public sealed class OpenRouterClaimReviewer(
    HttpClient http,
    IOptions<OpenRouterOptions> options,
    IOptions<ReviewOptions> reviewOptions,
    TimeProvider clock,
    ILogger<OpenRouterClaimReviewer> logger) : IClaimReviewer
{
    public string ModelName => options.Value.ReviewModel;

    /// <summary>Applies the base address; the review timeout is applied per review, so the HTTP client has none.</summary>
    public static void Configure(HttpClient client, OpenRouterOptions settings)
    {
        var baseUrl = settings.BaseUrl.TrimEnd(OpenRouterApi.PathSeparator) + OpenRouterApi.PathSeparator;
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<LlmClaimReview> ReviewAsync(
        ClaimPrompt prompt, ClaimEvaluationResult primary, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogWarning(
                "OpenRouter API key is not configured; set {ConfigKey} in user-secrets or an environment variable",
                $"{OpenRouterOptions.SectionName}:{nameof(OpenRouterOptions.ApiKey)}");
            throw new ChatFailedException(ChatFailureReasons.MissingApiKey);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(reviewOptions.Value.TimeoutSeconds));
        var request = ReviewWire.BuildRequest(settings.ReviewModel, prompt, primary);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var content = await OpenRouterChatTransport.SendAsync(http, request, settings, clock, logger, timeout.Token);
                if (ReviewWire.ParseReview(content, out var failureReason) is { } review)
                {
                    return review;
                }

                logger.LogWarning(
                    "OpenRouter review returned invalid output (attempt {Attempt}): {Reason}", attempt + 1, failureReason);
                if (attempt >= ChatApi.MalformedOutputRetries)
                {
                    throw new ChatFailedException(ChatFailureReasons.MalformedOutput);
                }
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("OpenRouter review timed out after {Seconds}s", reviewOptions.Value.TimeoutSeconds);
            throw new ChatFailedException(ChatFailureReasons.Timeout, ex);
        }
    }
}
