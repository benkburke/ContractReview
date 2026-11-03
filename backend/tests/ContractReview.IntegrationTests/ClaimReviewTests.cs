using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContractReview.Api.Routes;
using ContractReview.Domain.Errors;
using ContractReview.Infrastructure.Chat;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ContractReview.IntegrationTests;

/// <summary>Second-pass review through the API with the fake reviewer (Llm:Provider=Fake), per spec D1.</summary>
[Collection(ApiCollection.Name)]
public class ClaimReviewTests(ApiFactory factory)
{
    private static string ClaimsUrl(object contractId) => $"{ApiRoutes.Contracts}/{contractId}/claims";

    private static async Task<Guid> UploadAsync(HttpClient client, params string[] pages)
    {
        var form = new MultipartFormDataContent { { new StringContent($"Review-{Guid.NewGuid():N}"), "title" } };
        var part = new ByteArrayContent(TestPdfs.WithText(pages));
        part.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(part, "file", "contract.pdf");
        var upload = await client.PostAsync(ApiRoutes.Contracts, form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        return (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<Guid> UploadDefaultAsync(HttpClient client) =>
        UploadAsync(client, "COVERAGE SCHEDULE", "The radiator is covered.", "EXCLUSIONS", "Tires are excluded.");

    private static async Task<JsonElement> PostAsync(HttpClient client, Guid contractId, string text)
    {
        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = text });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Post_stores_a_completed_review_and_get_returns_the_same_review()
    {
        var client = factory.CreateClient();
        var contractId = await UploadDefaultAsync(client);

        var body = await PostAsync(client, contractId, "My radiator is leaking");

        var review = body.GetProperty("review");
        Assert.Equal("Completed", review.GetProperty("state").GetString());
        Assert.True(review.GetProperty("agreesWithPrimary").GetBoolean());
        Assert.Equal(FakeClaimReviewer.Model, review.GetProperty("model").GetString());
        Assert.Equal(FakeClaimReviewer.AgreeExplanation, review.GetProperty("accuracyExplanation").GetString());
        Assert.InRange(review.GetProperty("confidence").GetInt32(), 0, 100);

        var id = body.GetProperty("id").GetGuid();
        var fetched = await client.GetFromJsonAsync<JsonElement>($"{ClaimsUrl(contractId)}/{id}");
        Assert.Equal(review.GetRawText(), fetched.GetProperty("review").GetRawText());
    }

    [Fact]
    public async Task Disputed_claim_gets_a_review_that_disagrees()
    {
        var client = factory.CreateClient();
        var contractId = await UploadDefaultAsync(client);

        var body = await PostAsync(client, contractId, "My radiator is leaking, this is DISPUTED");

        var review = body.GetProperty("review");
        Assert.Equal("Completed", review.GetProperty("state").GetString());
        Assert.False(review.GetProperty("agreesWithPrimary").GetBoolean());
        Assert.Equal(FakeClaimReviewer.DisagreeConfidence, review.GetProperty("confidence").GetInt32());

        var id = body.GetProperty("id").GetGuid();
        var fetched = await client.GetFromJsonAsync<JsonElement>($"{ClaimsUrl(contractId)}/{id}");
        Assert.False(fetched.GetProperty("review").GetProperty("agreesWithPrimary").GetBoolean());
    }

    [Fact]
    public async Task Failing_review_is_201_with_a_Failed_review_and_the_primary_result_kept()
    {
        var client = factory.CreateClient();
        var contractId = await UploadDefaultAsync(client);

        var body = await PostAsync(client, contractId, "My radiator is leaking, Unreviewable");

        var result = body.GetProperty("result");
        Assert.NotEqual("InsufficientInformation", result.GetProperty("status").GetString());
        var review = body.GetProperty("review");
        Assert.Equal("Failed", review.GetProperty("state").GetString());
        Assert.Equal(ErrorCodes.ReviewFailed, review.GetProperty("errorCode").GetString());
        Assert.Equal(JsonValueKind.Null, review.GetProperty("agreesWithPrimary").ValueKind);

        var id = body.GetProperty("id").GetGuid();
        var fetched = await client.GetFromJsonAsync<JsonElement>($"{ClaimsUrl(contractId)}/{id}");
        Assert.Equal("Failed", fetched.GetProperty("review").GetProperty("state").GetString());
    }

    [Fact]
    public async Task Insufficient_information_has_no_review()
    {
        var client = factory.CreateClient();
        var contractId = await UploadDefaultAsync(client);

        var body = await PostAsync(client, contractId, "Something strange is happening, disputed");

        var result = body.GetProperty("result");
        Assert.Equal("InsufficientInformation", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("review").ValueKind);
    }

    [Fact]
    public async Task Needs_coverage_level_has_no_review()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(
            client, "COVERAGE SCHEDULE", "Coverage Level: ____.", "Coverage Levels: Powertrain, Gold, Platinum.", "The radiator is covered.");

        var body = await PostAsync(client, contractId, "My radiator is leaking");

        var result = body.GetProperty("result");
        Assert.True(result.GetProperty("needsCoverageLevel").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("review").ValueKind);
    }

    [Fact]
    public async Task Review_disabled_gives_no_review()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("Review:Enabled", "false")).CreateClient();
        var contractId = await UploadDefaultAsync(client);

        var body = await PostAsync(client, contractId, "My radiator is leaking");

        var result = body.GetProperty("result");
        Assert.NotEqual("InsufficientInformation", result.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("review").ValueKind);
    }
}
