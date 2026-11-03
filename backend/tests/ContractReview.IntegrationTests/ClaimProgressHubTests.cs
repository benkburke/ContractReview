using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContractReview.Api.Http;
using ContractReview.Api.Routes;
using ContractReview.Application.Features.Claims.Progress;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ContractReview.IntegrationTests;

/// <summary>A real SignalR client over the test server receives per-connection evaluation progress.</summary>
[Collection(ApiCollection.Name)]
public class ClaimProgressHubTests(ApiFactory factory)
{
    private static readonly string[] Pages =
    [
        "COVERAGE SCHEDULE",
        "The air conditioner is covered.",
        "EXCLUSIONS",
        "Tires are excluded.",
    ];

    private static string ClaimsUrl(Guid contractId) => $"{ApiRoutes.Contracts}/{contractId}/claims";

    private async Task<Guid> UploadAsync(HttpClient client)
    {
        var form = new MultipartFormDataContent { { new StringContent($"Progress-{Guid.NewGuid():N}"), "title" } };
        var part = new ByteArrayContent(TestPdfs.WithText(Pages));
        part.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
        form.Add(part, "file", "contract.pdf");
        var upload = await client.PostAsync(ApiRoutes.Contracts, form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        return (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<(HubConnection Connection, List<ClaimProgressEvent> Events)> ConnectAsync()
    {
        var events = new List<ClaimProgressEvent>();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, ApiRoutes.ClaimProgressHub), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
            .Build();
        connection.On<ClaimProgressEvent>(nameof(IClaimProgressClient.EvaluationProgress), e =>
        {
            lock (events) { events.Add(e); }
        });
        await connection.StartAsync();
        return (connection, events);
    }

    private static async Task<HttpResponseMessage> PostClaimAsync(HttpClient client, Guid contractId, string? connectionId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ClaimsUrl(contractId))
        {
            Content = JsonContent.Create(new { claimText = "Is the air conditioner covered?" }),
        };
        if (connectionId is not null)
        {
            request.Headers.Add(HeaderNames.SignalRConnectionId, connectionId);
        }

        return await client.SendAsync(request);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task Claim_with_connection_id_pushes_steps_in_order_to_that_connection_only()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);
        var (mine, myEvents) = await ConnectAsync();
        var (other, otherEvents) = await ConnectAsync();
        await using var _1 = mine;
        await using var _2 = other;

        var response = await PostClaimAsync(client, contractId, mine.ConnectionId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var claimId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await WaitForAsync(() => { lock (myEvents) { return myEvents.Count >= 3; } });
        await Task.Delay(300);

        lock (myEvents)
        {
            Assert.Equal(
                [ClaimProgressStep.Retrieving, ClaimProgressStep.Reasoning, ClaimProgressStep.Completed],
                myEvents.Select(e => e.Step).ToArray());
            Assert.All(myEvents, e => Assert.Equal(contractId, e.ContractId));
            Assert.Equal(claimId, myEvents[^1].ClaimId);
        }

        lock (otherEvents)
        {
            Assert.Empty(otherEvents);
        }
    }

    [Fact]
    public async Task Claim_with_unknown_connection_id_is_still_201()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);

        var response = await PostClaimAsync(client, contractId, "not-a-connected-id");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_claim_with_connection_id_pushes_no_steps()
    {
        var client = factory.CreateClient();
        var contractId = await UploadAsync(client);
        var (mine, myEvents) = await ConnectAsync();
        await using var _ = mine;

        var request = new HttpRequestMessage(HttpMethod.Post, ClaimsUrl(contractId))
        {
            Content = JsonContent.Create(new { claimText = "abc" }),
        };
        request.Headers.Add(HeaderNames.SignalRConnectionId, mine.ConnectionId);
        var response = await client.SendAsync(request);
        await Task.Delay(300);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        lock (myEvents)
        {
            Assert.Empty(myEvents);
        }
    }
}
