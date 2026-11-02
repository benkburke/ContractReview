using System.Net;
using ContractReview.Api.Routes;

namespace ContractReview.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(ApiRoutes.Health);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
