using System.Net;

namespace MailTemplateHub.IntegrationTests;

public class SecurityHeadersTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Baseline_security_headers_are_present()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", First(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", First(response, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", First(response, "Referrer-Policy"));
    }

    private static string? First(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}
