using System.Net;
using ContractReview.Api.Routes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ContractReview.IntegrationTests;

public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(ApiRoutes.Health);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
