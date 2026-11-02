using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContractReview.Api.Routes;
using ContractReview.Application.Abstractions.Embeddings;
using ContractReview.Domain.Contracts;
using ContractReview.Domain.Errors;
using ContractReview.Infrastructure.Indexing;
using ContractReview.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ContractReview.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ContractIndexingTests(ApiFactory factory)
{
    private const string Indexed = "Indexed";
    private const string Failed = "Failed";

    private static readonly string[] CoveragePages =
    [
        "SCHEDULE OF COVERAGE",
        "The engine is covered for five years.",
        "WHAT IS NOT COVERED",
        "Wear and tear is excluded.",
    ];

    private sealed class SwitchableEmbedder : IEmbeddingClient
    {
        public volatile bool Fail;

        public string ModelName => "stub-model";

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new EmbeddingFailedException("stub failure");
            }

            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new float[] { 1f, 0f, 0f }).ToList());
        }
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static MultipartFormDataContent Form(string title, byte[] file)
    {
        var form = new MultipartFormDataContent { { new StringContent(title), "title" } };
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(part, "file", "contract.pdf");
        return form;
    }

    private static async Task<JsonElement> UploadAsync(HttpClient client)
    {
        var response = await client.PostAsync(ApiRoutes.Contracts, Form(Unique("Idx"), TestPdfs.WithText(CoveragePages)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string IndexUrl(Guid id) => $"{ApiRoutes.Contracts}/{id}/index";

    private (HttpClient Client, SwitchableEmbedder Embedder) StubHost()
    {
        var embedder = new SwitchableEmbedder();
        var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IEmbeddingClient>(embedder)));
        return (host.CreateClient(), embedder);
    }

    private async Task<Contract> LoadAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Contracts.AsNoTracking().Include(c => c.Chunks).SingleAsync(c => c.Id == id);
    }

    private async Task<Guid> SeedAsync(bool indexing)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contract = Contract.Upload(
            Unique("Seed"), "seed.pdf", "application/pdf", [1, 2, 3], "SCHEDULE OF COVERAGE\n\nEngine covered.", 1, 1000, DateTime.UtcNow);
        if (indexing)
        {
            contract.StartIndexing();
        }

        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return contract.Id;
    }

    [Fact]
    public async Task Upload_indexes_contract_and_detail_lists_sections_in_order()
    {
        var client = factory.CreateClient();

        var summary = await UploadAsync(client);

        Assert.Equal(Indexed, summary.GetProperty("indexStatus").GetString());
        var id = summary.GetProperty("id").GetGuid();
        var detail = await client.GetFromJsonAsync<JsonElement>($"{ApiRoutes.Contracts}/{id}");
        Assert.Equal(Indexed, detail.GetProperty("indexStatus").GetString());
        var sections = detail.GetProperty("sections").EnumerateArray().ToList();
        Assert.Equal(
            ["SCHEDULE OF COVERAGE", "WHAT IS NOT COVERED"],
            sections.Select(s => s.GetProperty("heading").GetString()!).ToArray());
        Assert.Equal([0, 1], sections.Select(s => s.GetProperty("order").GetInt32()).ToArray());
        Assert.All(sections, s => Assert.True(s.GetProperty("chunkCount").GetInt32() >= 1));
        Assert.False(sections[0].TryGetProperty("embedding", out _));

        var stored = await LoadAsync(id);
        Assert.Equal(IndexStatus.Indexed, stored.IndexStatus);
        Assert.NotEmpty(stored.Chunks);
        Assert.All(stored.Chunks, c => Assert.NotEmpty(c.Embedding));
        Assert.NotNull(stored.EmbeddingDimensions);
    }

    [Fact]
    public async Task List_includes_index_status()
    {
        var client = factory.CreateClient();
        var id = (await UploadAsync(client)).GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<JsonElement>(ApiRoutes.Contracts);

        var item = list.EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == id);
        Assert.Equal(Indexed, item.GetProperty("indexStatus").GetString());
    }

    [Fact]
    public async Task Upload_returns_201_and_keeps_contract_as_Failed_when_embedding_fails()
    {
        var (client, embedder) = StubHost();
        embedder.Fail = true;

        var summary = await UploadAsync(client);

        Assert.Equal(Failed, summary.GetProperty("indexStatus").GetString());
        var id = summary.GetProperty("id").GetGuid();
        var detail = await client.GetFromJsonAsync<JsonElement>($"{ApiRoutes.Contracts}/{id}");
        Assert.Equal(Failed, detail.GetProperty("indexStatus").GetString());
        Assert.Contains("engine", detail.GetProperty("extractedText").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(detail.GetProperty("sections").EnumerateArray());
        var stored = await LoadAsync(id);
        Assert.Equal(ErrorCodes.ContractEmbeddingFailed, stored.IndexError);
        Assert.Empty(stored.Chunks);
    }

    [Fact]
    public async Task Retry_returns_502_when_embedding_still_fails_then_200_when_it_recovers()
    {
        var (client, embedder) = StubHost();
        embedder.Fail = true;
        var id = (await UploadAsync(client)).GetProperty("id").GetGuid();

        var failed = await client.PostAsync(IndexUrl(id), null);
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal(
            ErrorCodes.ContractEmbeddingFailed,
            (await failed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(IndexStatus.Failed, (await LoadAsync(id)).IndexStatus);

        embedder.Fail = false;
        var ok = await client.PostAsync(IndexUrl(id), null);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Indexed, body.GetProperty("indexStatus").GetString());
        Assert.NotEmpty(body.GetProperty("sections").EnumerateArray());
        Assert.Equal(IndexStatus.Indexed, (await LoadAsync(id)).IndexStatus);
    }

    [Fact]
    public async Task Retry_returns_404_when_contract_is_unknown()
    {
        var response = await factory.CreateClient().PostAsync(IndexUrl(Guid.NewGuid()), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            ErrorCodes.ContractNotFound,
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Retry_returns_409_when_contract_is_Indexing()
    {
        var id = await SeedAsync(indexing: true);

        var response = await factory.CreateClient().PostAsync(IndexUrl(id), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            ErrorCodes.ContractIndexingInProgress,
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(IndexStatus.Indexing, (await LoadAsync(id)).IndexStatus);
    }

    [Fact]
    public async Task Retry_on_NotIndexed_contract_indexes_it()
    {
        var id = await SeedAsync(indexing: false);

        var response = await factory.CreateClient().PostAsync(IndexUrl(id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(IndexStatus.Indexed, (await LoadAsync(id)).IndexStatus);
    }

    [Fact]
    public async Task Reindex_replaces_chunks_without_mixing_old_and_new()
    {
        var client = factory.CreateClient();
        var id = (await UploadAsync(client)).GetProperty("id").GetGuid();
        var before = await LoadAsync(id);

        var response = await client.PostAsync(IndexUrl(id), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await LoadAsync(id);
        Assert.Equal(before.Chunks.Count, after.Chunks.Count);
        Assert.Empty(before.Chunks.Select(c => c.Id).Intersect(after.Chunks.Select(c => c.Id)));
        Assert.Equal(
            after.Chunks.Count, after.Chunks.Select(c => (c.SectionOrder, c.ChunkOrder)).Distinct().Count());
    }

    [Fact]
    public async Task Failed_reindex_keeps_previous_chunks()
    {
        var (client, embedder) = StubHost();
        var id = (await UploadAsync(client)).GetProperty("id").GetGuid();
        var before = await LoadAsync(id);
        Assert.NotEmpty(before.Chunks);

        embedder.Fail = true;
        var response = await client.PostAsync(IndexUrl(id), null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var after = await LoadAsync(id);
        Assert.Equal(IndexStatus.Failed, after.IndexStatus);
        Assert.Equal(
            before.Chunks.Select(c => c.Id).Order().ToArray(), after.Chunks.Select(c => c.Id).Order().ToArray());
    }

    [Fact]
    public async Task Backfill_indexes_NotIndexed_Failed_and_stale_Indexing_contracts()
    {
        var notIndexed = await SeedAsync(indexing: false);
        var stale = await SeedAsync(indexing: true);
        var (stubClient, embedder) = StubHost();
        embedder.Fail = true;
        var failed = (await UploadAsync(stubClient)).GetProperty("id").GetGuid();
        Assert.Equal(IndexStatus.Failed, (await LoadAsync(failed)).IndexStatus);

        var service = factory.Services.GetServices<IHostedService>().OfType<ContractIndexBackfillService>().Single();
        await service.RunAsync(CancellationToken.None);

        foreach (var id in new[] { notIndexed, stale, failed })
        {
            var contract = await LoadAsync(id);
            Assert.Equal(IndexStatus.Indexed, contract.IndexStatus);
            Assert.NotEmpty(contract.Chunks);
        }
    }
}
