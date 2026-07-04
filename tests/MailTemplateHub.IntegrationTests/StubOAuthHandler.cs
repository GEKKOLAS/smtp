using System.Net;
using System.Text.Json;

namespace MailTemplateHub.IntegrationTests;

/// <summary>
/// In-process double for the Google/Microsoft OAuth + profile endpoints, matched
/// by request URL. Tests script token and profile responses without a live
/// provider (spec 11-testing.md). Shared across all HttpClients in the factory.
/// </summary>
public sealed class StubOAuthHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _rules = [];

    public List<(string Method, string Url)> Requests { get; } = [];

    public void OnPost(string urlContains, object json, HttpStatusCode status = HttpStatusCode.OK) =>
        _rules.Add((
            r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().Contains(urlContains),
            _ => JsonResponse(json, status)));

    public void OnGet(string urlContains, object json, HttpStatusCode status = HttpStatusCode.OK) =>
        _rules.Add((
            r => r.Method == HttpMethod.Get && r.RequestUri!.ToString().Contains(urlContains),
            _ => JsonResponse(json, status)));

    public void Reset()
    {
        _rules.Clear();
        Requests.Clear();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method.Method, request.RequestUri!.ToString()));
        foreach (var (match, respond) in _rules)
        {
            if (match(request)) return Task.FromResult(respond(request));
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotImplemented)
        {
            Content = new StringContent($"No stub rule for {request.Method} {request.RequestUri}"),
        });
    }

    private static HttpResponseMessage JsonResponse(object json, HttpStatusCode status) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(json), System.Text.Encoding.UTF8, "application/json"),
    };
}
