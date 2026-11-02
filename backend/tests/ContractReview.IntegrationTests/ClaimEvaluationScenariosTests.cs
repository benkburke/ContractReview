using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContractReview.Api.Routes;
using ContractReview.Application.Abstractions.Chat;
using ContractReview.Domain.Claims;
using ContractReview.Domain.Errors;
using ContractReview.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ContractReview.IntegrationTests;

/// <summary>Thin-mode critical scenarios for T3: coverage-level question, failed evaluation, citation downgrade.</summary>
[Collection(ApiCollection.Name)]
public class ClaimEvaluationScenariosTests(ApiFactory factory)
{
    private static string ClaimsUrl(object contractId) => $"{ApiRoutes.Contracts}/{contractId}/claims";

    private static async Task<Guid> UploadAsync(HttpClient client, params string[] pages)
    {
        var form = new MultipartFormDataContent { { new StringContent($"Scenario-{Guid.NewGuid():N}"), "title" } };
        var part = new ByteArrayContent(TestPdfs.WithText(pages));
        part.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(part, "file", "contract.pdf");
        var upload = await client.PostAsync(ApiRoutes.Contracts, form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Indexed", created.GetProperty("indexStatus").GetString());
        return created.GetProperty("id").GetGuid();
    }

    private HttpClient ClientWithChat(IChatCompletionClient chat) =>
        factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IChatCompletionClient>();
            s.AddSingleton(chat);
        })).CreateClient();

    private sealed class StubChat(Func<ClaimPrompt, LlmClaimAnswer> answer) : IChatCompletionClient
    {
        public string ModelName => "stub/model";

        public Task<LlmClaimAnswer> EvaluateAsync(ClaimPrompt prompt, CancellationToken cancellationToken) =>
            Task.FromResult(answer(prompt));
    }

    private sealed class ThrowingChat : IChatCompletionClient
    {
        public string ModelName => "stub/model";

        public Task<LlmClaimAnswer> EvaluateAsync(ClaimPrompt prompt, CancellationToken cancellationToken) =>
            throw new ChatFailedException(ChatFailureReasons.Timeout);
    }

    private static LlmClaimAnswer Answer(ClaimStatus status, params string[] labels) =>
        new(status, 90, null, false, [], [], [], "Because.", null, labels);

    [Fact]
    public async Task Blank_coverage_level_asks_for_a_level_and_evaluate_again_uses_the_chosen_one()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(
            client, "COVERAGE SCHEDULE", "Coverage Level: ____.", "Coverage Levels: Powertrain, Gold, Platinum.", "The radiator is covered.");

        var first = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "My radiator is leaking" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var result = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result");
        Assert.True(result.GetProperty("needsCoverageLevel").GetBoolean());
        Assert.Equal("InsufficientInformation", result.GetProperty("status").GetString());
        var options = result.GetProperty("coverageLevelOptions").EnumerateArray().Select(o => o.GetString() ?? "").ToArray();
        Assert.Equal(["Powertrain", "Gold", "Platinum"], options);

        var second = await client.PostAsJsonAsync(
            ClaimsUrl(contractId), new { claimText = "My radiator is leaking", coverageLevel = "Gold" });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Gold", body.GetProperty("coverageLevelRequested").GetString());
        var result2 = body.GetProperty("result");
        Assert.False(result2.GetProperty("needsCoverageLevel").GetBoolean());
        Assert.Equal("Gold", result2.GetProperty("coverageLevel").GetString());
        Assert.NotEqual("InsufficientInformation", result2.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Chat_failure_is_502_and_a_Failed_claim_is_stored_without_a_result()
    {
        var client = ClientWithChat(new ThrowingChat());
        var contractId = await UploadAsync(client, "COVERAGE SCHEDULE", "The radiator is covered.");

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "My radiator is leaking" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ErrorCodes.ClaimEvaluationFailed, problem.GetProperty("code").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var claims = await db.Claims.AsNoTracking().Where(c => c.ContractId == contractId).ToListAsync();
        var stored = Assert.Single(claims);
        Assert.Equal(ClaimState.Failed, stored.State);
        Assert.Equal(ErrorCodes.ClaimEvaluationFailed, stored.ErrorCode);
        Assert.Null(stored.Result);

        var fetched = await client.GetAsync($"{ClaimsUrl(contractId)}/{stored.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var dto = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Failed", dto.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task Malformed_chat_output_is_502()
    {
        var client = ClientWithChat(new StubChat(_ => Answer(ClaimStatus.Covered, "C1") with { Confidence = 101 }));
        var contractId = await UploadAsync(client, "COVERAGE SCHEDULE", "The radiator is covered.");

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "My radiator is leaking" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Theory]
    [InlineData(ClaimStatus.Covered)]
    [InlineData(ClaimStatus.NotCovered)]
    [InlineData(ClaimStatus.Conditional)]
    public async Task Decisive_answer_without_a_retrieved_citation_is_stored_as_InsufficientInformation(ClaimStatus status)
    {
        var client = ClientWithChat(new StubChat(_ => Answer(status, "C99", "nonsense")));
        var contractId = await UploadAsync(client, "COVERAGE SCHEDULE", "The radiator is covered.");

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "My radiator is leaking" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result");
        Assert.Equal("InsufficientInformation", result.GetProperty("status").GetString());
        Assert.Equal(0, result.GetProperty("citations").GetArrayLength());
    }

    [Fact]
    public async Task Only_retrieved_chunks_are_kept_as_citations()
    {
        var client = ClientWithChat(new StubChat(_ => Answer(ClaimStatus.Covered, "C99", "C1")));
        var contractId = await UploadAsync(client, "COVERAGE SCHEDULE", "The radiator is covered.");

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "My radiator is leaking" });

        var result = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result");
        Assert.Equal("Covered", result.GetProperty("status").GetString());
        var citations = result.GetProperty("citations");
        Assert.Equal(1, citations.GetArrayLength());
        Assert.Equal("COVERAGE SCHEDULE", citations[0].GetProperty("sectionHeading").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"claimText\":null}")]
    [InlineData("{\"claimText\":\"     \"}")]
    public async Task Missing_or_blank_claim_text_is_400(string json)
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client, "COVERAGE SCHEDULE", "The radiator is covered.");

        var response = await client.PostAsync(ClaimsUrl(contractId), new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Claim_text_over_2000_characters_is_400()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client, "COVERAGE SCHEDULE", "The radiator is covered.");

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = new string('a', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            ErrorCodes.ClaimTextInvalid,
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
