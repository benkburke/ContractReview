using System.Net;
using System.Text;
using System.Text.Json;
using ContractReview.Application.Abstractions.Chat;
using ContractReview.Domain.Claims;
using ContractReview.Infrastructure.Chat;
using ContractReview.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractReview.UnitTests.Infrastructure;

public class OpenRouterChatClientTests
{
    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var body = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static string AnswerJson(bool needsLevel, string status = "InsufficientInformation") => JsonSerializer.Serialize(new
    {
        status,
        confidence = 40,
        coverageLevel = (string?)null,
        needsCoverageLevel = needsLevel,
        coverageLevelOptions = Array.Empty<string>(),
        coveredItems = Array.Empty<string>(),
        excludedItems = Array.Empty<string>(),
        reasoning = "Not enough information.",
        followUpQuestion = (string?)null,
        citations = Array.Empty<string>(),
    });

    private static OpenRouterChatClient CreateClient(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://example.test/api/v1/") },
        Options.Create(new OpenRouterOptions { ApiKey = "test-key" }),
        TimeProvider.System,
        NullLogger<OpenRouterChatClient>.Instance);

    private static readonly ClaimPrompt Prompt = new("My phone screen cracked", null, [new PromptChunk("C1", "Coverage", "Screens are covered.")]);

    [Fact]
    public async Task EvaluateAsync_NeedsLevelWithoutOptions_ReturnsAnswerWithNeedsLevelFalse()
    {
        var handler = new StubHandler(AnswerJson(needsLevel: true));

        var answer = await CreateClient(handler).EvaluateAsync(Prompt, CancellationToken.None);

        Assert.False(answer.NeedsCoverageLevel);
        Assert.Empty(answer.CoverageLevelOptions);
        Assert.Equal(ClaimStatus.InsufficientInformation, answer.Status);
    }

    [Fact]
    public async Task EvaluateAsync_NoLevelNeededAndNoOptions_ReturnsAnswerOnFirstAttempt()
    {
        var handler = new StubHandler(AnswerJson(needsLevel: false));

        var answer = await CreateClient(handler).EvaluateAsync(Prompt, CancellationToken.None);

        Assert.False(answer.NeedsCoverageLevel);
        Assert.Equal(1, handler.Calls);
    }
}
