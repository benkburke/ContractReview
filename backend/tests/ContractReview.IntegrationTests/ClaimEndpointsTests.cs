using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContractReview.Api.Routes;
using ContractReview.Domain.Contracts;
using ContractReview.Domain.Errors;
using ContractReview.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace ContractReview.IntegrationTests;

/// <summary>Smoke tests of the claim endpoints with the fake embedder and the fake chat client.</summary>
[Collection(ApiCollection.Name)]
public class ClaimEndpointsTests(ApiFactory factory)
{
    private static readonly string[] Pages =
    [
        "COVERAGE SCHEDULE",
        "The air conditioner is covered.",
        "EXCLUSIONS",
        "Tires are excluded.",
    ];

    private static string ClaimsUrl(object contractId) => $"{ApiRoutes.Contracts}/{contractId}/claims";

    /// <summary>Uploading indexes the contract at once with the fake embedder.</summary>
    private static async Task<Guid> UploadAsync(HttpClient client)
    {
        var form = new MultipartFormDataContent { { new StringContent($"Claims-{Guid.NewGuid():N}"), "title" } };
        var part = new ByteArrayContent(TestPdfs.WithText(Pages));
        part.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(part, "file", "contract.pdf");
        var upload = await client.PostAsync(ApiRoutes.Contracts, form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Indexed", created.GetProperty("indexStatus").GetString());
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    [Theory]
    [InlineData("Is the air conditioner covered?", "Covered", "COVERAGE SCHEDULE")]
    [InlineData("I have a flat tire", "NotCovered", "EXCLUSIONS")]
    public async Task Evaluate_stores_and_returns_a_cited_decision(string claimText, string status, string heading)
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = created.GetProperty("result");
        Assert.Equal("Evaluated", created.GetProperty("state").GetString());
        Assert.Equal(status, result.GetProperty("status").GetString());
        Assert.Equal(heading, result.GetProperty("citations")[0].GetProperty("sectionHeading").GetString());

        var fetched = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var stored = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(created.GetProperty("id").GetGuid(), stored.GetProperty("id").GetGuid());
        Assert.Equal(status, stored.GetProperty("result").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Vague_claim_is_insufficient_information_with_a_follow_up_question()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "My car sounds weird" });

        var result = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result");
        Assert.Equal("InsufficientInformation", result.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("followUpQuestion").GetString()));
    }

    [Fact]
    public async Task Invalid_text_is_400_and_unknown_ids_are_404()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var invalid = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "abc" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(ErrorCodes.ClaimTextInvalid, await CodeAsync(invalid));

        var badLevel = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "Flat tire", coverageLevel = "  " });
        Assert.Equal(ErrorCodes.ClaimCoverageLevelInvalid, await CodeAsync(badLevel));

        var noContract = await client.PostAsJsonAsync(ClaimsUrl(Guid.NewGuid()), new { claimText = "Flat tire" });
        Assert.Equal(HttpStatusCode.NotFound, noContract.StatusCode);
        Assert.Equal(ErrorCodes.ContractNotFound, await CodeAsync(noContract));

        var getNoContract = await client.GetAsync($"{ClaimsUrl(Guid.NewGuid())}/{Guid.NewGuid()}");
        Assert.Equal(ErrorCodes.ContractNotFound, await CodeAsync(getNoContract));

        var getNoClaim = await client.GetAsync($"{ClaimsUrl(contractId)}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, getNoClaim.StatusCode);
        Assert.Equal(ErrorCodes.ClaimNotFound, await CodeAsync(getNoClaim));
    }

    [Fact]
    public async Task Contract_that_is_not_indexed_is_409()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);
        await UpdateContractAsync(contractId, s => s.SetProperty(c => c.IndexStatus, IndexStatus.NotIndexed));

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "Flat tire" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.ContractNotIndexed, await CodeAsync(response));
    }

    [Fact]
    public async Task Contract_indexed_with_another_embedding_model_is_409()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);
        await UpdateContractAsync(contractId, s => s.SetProperty(c => c.EmbeddingModel, "other/model"));

        var response = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "Flat tire" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.ContractEmbeddingModelMismatch, await CodeAsync(response));
    }

    private async Task UpdateContractAsync(
        Guid id, Action<UpdateSettersBuilder<Contract>> set)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Contracts.Where(c => c.Id == id).ExecuteUpdateAsync(set);
    }
}
