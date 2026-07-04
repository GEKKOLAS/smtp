using System.Net;

namespace MailTemplateHub.IntegrationTests;

public class HealthzTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Healthz_returns_200_healthy_with_database_up()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
