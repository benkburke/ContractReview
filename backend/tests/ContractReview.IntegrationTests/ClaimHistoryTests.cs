using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContractReview.Api.Routes;
using ContractReview.Domain.Claims;
using ContractReview.Domain.Errors;
using ContractReview.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ContractReview.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ClaimHistoryTests(ApiFactory factory)
{
    private static string ClaimsUrl(object contractId) => $"{ApiRoutes.Contracts}/{contractId}/claims";

    private static async Task<Guid> UploadAsync(HttpClient client)
    {
        var form = new MultipartFormDataContent { { new StringContent($"History-{Guid.NewGuid():N}"), "title" } };
        var part = new ByteArrayContent(TestPdfs.WithText(["COVERAGE SCHEDULE", "The air conditioner is covered."]));
        part.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(part, "file", "contract.pdf");
        var upload = await client.PostAsync(ApiRoutes.Contracts, form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        return (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Lists_newest_first_and_pages()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);
        for (var i = 1; i <= 3; i++)
        {
            var post = await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = $"Is the air conditioner covered? #{i}" });
            Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        }

        var first = await client.GetFromJsonAsync<JsonElement>($"{ClaimsUrl(contractId)}?page=1&pageSize=2");
        Assert.Equal(3, first.GetProperty("totalCount").GetInt32());
        var items = first.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.EndsWith("#3", items[0].GetProperty("claimText").GetString());
        Assert.EndsWith("#2", items[1].GetProperty("claimText").GetString());
        Assert.Equal("Evaluated", items[0].GetProperty("state").GetString());

        var second = await client.GetFromJsonAsync<JsonElement>($"{ClaimsUrl(contractId)}?page=2&pageSize=2");
        Assert.Equal(1, second.GetProperty("items").GetArrayLength());
        Assert.EndsWith("#1", second.GetProperty("items")[0].GetProperty("claimText").GetString());
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(10001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Invalid_paging_is_400(int page, int pageSize)
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var response = await client.GetAsync($"{ClaimsUrl(contractId)}?page={page}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var code = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
        Assert.Equal(ErrorCodes.PagingInvalid, code);
    }

    [Fact]
    public async Task Unknown_contract_is_404()
    {
        var response = await factory.CreateClient().GetAsync(ClaimsUrl(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var code = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
        Assert.Equal(ErrorCodes.ContractNotFound, code);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(10000, 20)]
    [InlineData(1, 20)]
    public async Task Valid_paging_boundaries_are_200_and_echo_the_values(int page, int pageSize)
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var response = await client.GetAsync($"{ClaimsUrl(contractId)}?page={page}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(page, body.GetProperty("page").GetInt32());
        Assert.Equal(pageSize, body.GetProperty("pageSize").GetInt32());
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(1, -1)]
    [InlineData(int.MaxValue, 20)]
    public async Task Negative_or_huge_paging_is_400_with_paging_invalid(int page, int pageSize)
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var response = await client.GetAsync($"{ClaimsUrl(contractId)}?page={page}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var code = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
        Assert.Equal(ErrorCodes.PagingInvalid, code);
    }

    [Theory]
    [InlineData("page=abc")]
    [InlineData("pageSize=x")]
    [InlineData("page=1.5")]
    public async Task Non_integer_paging_is_400(string query)
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var response = await client.GetAsync($"{ClaimsUrl(contractId)}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Page_past_the_end_returns_empty_items_with_the_correct_total()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);
        for (var i = 1; i <= 3; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = $"Is the air conditioner covered? #{i}" })).StatusCode);
        }

        var body = await client.GetFromJsonAsync<JsonElement>($"{ClaimsUrl(contractId)}?page=3&pageSize=2");

        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        Assert.Equal(3, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, body.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task Empty_history_returns_zero_total_and_no_items()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var body = await client.GetFromJsonAsync<JsonElement>(ClaimsUrl(contractId));

        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Ties_on_createdAtUtc_order_by_id_descending_and_failed_claims_have_null_status()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);
        var when = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var ids = new List<Guid>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 5; i++)
            {
                var claim = Claim.Start(contractId, $"Tied failed claim number {i}", null, when);
                claim.Fail(ErrorCodes.ClaimEvaluationFailed, when);
                db.Claims.Add(claim);
                ids.Add(claim.Id);
            }

            await db.SaveChangesAsync();
        }

        var body = await client.GetFromJsonAsync<JsonElement>(ClaimsUrl(contractId));

        // SQL Server orders uniqueidentifier differently from .NET Guid, so compare with SqlGuid ordering.
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(ids.OrderByDescending(x => new System.Data.SqlTypes.SqlGuid(x)).ToList(), items.Select(i => i.GetProperty("id").GetGuid()).ToList());
        Assert.All(items, i =>
        {
            Assert.Equal("Failed", i.GetProperty("state").GetString());
            Assert.Equal(JsonValueKind.Null, i.GetProperty("status").ValueKind);
            Assert.Equal(JsonValueKind.Null, i.GetProperty("confidence").ValueKind);
            Assert.Equal(JsonValueKind.Null, i.GetProperty("reviewAgrees").ValueKind);
        });
    }

    [Fact]
    public async Task Evaluated_claims_expose_status_confidence_and_reviewAgrees()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "Is the air conditioner covered? agrees" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "Is the air conditioner covered? disputed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(ClaimsUrl(contractId), new { claimText = "Is the air conditioner covered? unreviewable" })).StatusCode);

        var items = (await client.GetFromJsonAsync<JsonElement>(ClaimsUrl(contractId))).GetProperty("items");

        Assert.Equal(3, items.GetArrayLength());
        Assert.EndsWith("unreviewable", items[0].GetProperty("claimText").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("reviewAgrees").ValueKind);
        Assert.EndsWith("disputed", items[1].GetProperty("claimText").GetString());
        Assert.False(items[1].GetProperty("reviewAgrees").GetBoolean());
        Assert.True(items[2].GetProperty("reviewAgrees").GetBoolean());
        Assert.All(items.EnumerateArray(), i =>
        {
            Assert.Equal("Evaluated", i.GetProperty("state").GetString());
            Assert.NotEqual(JsonValueKind.Null, i.GetProperty("status").ValueKind);
            Assert.InRange(i.GetProperty("confidence").GetInt32(), 0, 100);
        });
    }
}
