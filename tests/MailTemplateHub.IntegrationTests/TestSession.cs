using System.Net.Http.Json;

namespace MailTemplateHub.IntegrationTests;

/// <summary>
/// Minimal cookie jar + CSRF echo for tests. Cookie handling is manual (the
/// factory client has HandleCookies disabled) so assertions can inspect and
/// tamper with cookies and the CSRF header explicitly.
/// </summary>
public sealed class TestSession(HttpClient client)
{
    public Dictionary<string, string> Cookies { get; } = [];

    public string? CsrfToken => Cookies.GetValueOrDefault("mth_csrf");
    public bool HasSessionCookie => Cookies.ContainsKey("mth_session");

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, object? body = null, bool includeCsrfHeader = true)
    {
        using var request = new HttpRequestMessage(method, url);
        if (Cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", Cookies.Select(c => $"{c.Key}={c.Value}")));
        }
        if (method != HttpMethod.Get && includeCsrfHeader && CsrfToken is not null)
        {
            request.Headers.Add("X-CSRF-Token", CsrfToken);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await client.SendAsync(request);
        Capture(response);
        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url);
    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);

    private void Capture(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies)) return;

        foreach (var setCookie in setCookies)
        {
            var nameValue = setCookie.Split(';')[0];
            var separator = nameValue.IndexOf('=');
            if (separator <= 0) continue;

            var name = nameValue[..separator];
            var value = nameValue[(separator + 1)..];
            if (string.IsNullOrEmpty(value))
            {
                Cookies.Remove(name);
            }
            else
            {
                Cookies[name] = value;
            }
        }
    }
}
