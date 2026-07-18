using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MailTemplateHub.Application.Abstractions.Oauth;
using MailTemplateHub.Application.Common;
using MailTemplateHub.Domain.Enums;

namespace MailTemplateHub.Infrastructure.Providers;

/// <summary>
/// Shared OAuth 2.0 authorization-code + PKCE plumbing for Google and Microsoft
/// (spec 07-providers.md). Endpoints come from options so tests target a double.
/// </summary>
internal abstract class OAuthProviderServiceBase(HttpClient httpClient, OAuthProviderOptions options)
    : IOAuthProviderService
{
    protected HttpClient Http { get; } = httpClient;
    protected OAuthProviderOptions Options { get; } = options;

    public abstract EmailProvider Provider { get; }
    public abstract IReadOnlyList<string> RequiredScopes { get; }

    public virtual string BuildAuthorizationUrl(string state, string codeChallenge, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = Options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', Options.Scopes),
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };
        foreach (var (key, value) in ExtraAuthorizationParameters())
        {
            query[key] = value;
        }
        return QueryHelpers.AddQuery(Options.AuthorizationEndpoint, query);
    }

    protected virtual IEnumerable<KeyValuePair<string, string?>> ExtraAuthorizationParameters() => [];

    public async Task<OAuthTokenResponse> ExchangeCodeAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = Options.ClientId,
            ["client_secret"] = Options.ClientSecret,
            ["code_verifier"] = codeVerifier,
        };
        return await PostTokenAsync(form, isRefresh: false, ct);
    }

    public async Task<OAuthTokenResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = Options.ClientId,
            ["client_secret"] = Options.ClientSecret,
        };
        foreach (var (key, value) in ExtraRefreshParameters())
        {
            form[key] = value;
        }
        return await PostTokenAsync(form, isRefresh: true, ct);
    }

    protected virtual IEnumerable<KeyValuePair<string, string>> ExtraRefreshParameters() => [];

    private async Task<OAuthTokenResponse> PostTokenAsync(
        Dictionary<string, string> form, bool isRefresh, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await Http.PostAsync(Options.TokenEndpoint, new FormUrlEncodedContent(form), ct);
        }
        catch (HttpRequestException ex)
        {
            throw new OAuthTransientException("The provider token endpoint was unreachable.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new OAuthTransientException("The provider token endpoint timed out.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode >= 500)
            {
                throw new OAuthTransientException($"Provider token endpoint returned {(int)response.StatusCode}.");
            }

            var error = TryReadError(body);
            if (isRefresh && error is "invalid_grant" or "invalid_token")
            {
                throw new RefreshTokenRevokedException(error);
            }
            throw new OAuthExchangeException($"Token request failed ({error ?? response.StatusCode.ToString()}).");
        }

        return ParseTokenResponse(body);
    }

    private OAuthTokenResponse ParseTokenResponse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new OAuthExchangeException("Token response is missing access_token.");
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var idToken = root.TryGetProperty("id_token", out var it) ? it.GetString() : null;

        var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.TryGetInt32(out var seconds)
            ? seconds
            : 3600;

        var scopes = root.TryGetProperty("scope", out var sc) && sc.GetString() is { } scopeString
            ? scopeString.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : Options.Scopes;

        return new OAuthTokenResponse(
            accessToken,
            refreshToken,
            DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            scopes,
            idToken);
    }

    public abstract Task<ProviderProfile> GetProfileAsync(string accessToken, string? idToken, CancellationToken ct);

    protected async Task<JsonElement> GetJsonAsync(string url, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new OAuthTransientException("The provider profile endpoint was unreachable.", ex);
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new OAuthTransientException($"Provider profile endpoint returned {(int)response.StatusCode}.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new OAuthExchangeException($"Profile request failed ({(int)response.StatusCode}).");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
    }

    public virtual async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(Options.RevokeEndpoint)) return;
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken });
            await Http.PostAsync(Options.RevokeEndpoint, content, ct);
        }
        catch
        {
            // Best-effort; caller wipes local tokens regardless.
        }
    }

    private static string? TryReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    protected static string? StringOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
