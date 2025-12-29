namespace MGF.Worker.Integrations.Dropbox;

using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public interface IDropboxAccessTokenProvider
{
    Task<DropboxAccessTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public sealed record DropboxAccessTokenResult(
    string? AccessToken,
    string AuthMode,
    string Source,
    string? Error);

internal sealed class DropboxAccessTokenProvider : IDropboxAccessTokenProvider
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private static string? CachedToken;
    private static DateTimeOffset CachedExpiresAt;

    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger? logger;

    public DropboxAccessTokenProvider(HttpClient httpClient, IConfiguration configuration, ILogger? logger = null)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        this.httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<DropboxAccessTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var access = ResolveValue(
            configuration,
            primaryKey: "Integrations:Dropbox:AccessToken",
            fallbackKey: "Dropbox:AccessToken",
            envPrimary: "Integrations__Dropbox__AccessToken",
            envFallback: "Dropbox__AccessToken");

        if (!string.IsNullOrWhiteSpace(access.Value))
        {
            LogAuthMode("access_token", access.Source);
            return new DropboxAccessTokenResult(access.Value, "access_token", access.Source, null);
        }

        var refresh = ResolveValue(
            configuration,
            primaryKey: "Integrations:Dropbox:RefreshToken",
            fallbackKey: "Dropbox:RefreshToken",
            envPrimary: "Integrations__Dropbox__RefreshToken",
            envFallback: "Dropbox__RefreshToken");

        if (!string.IsNullOrWhiteSpace(refresh.Value))
        {
            return await GetFromRefreshTokenAsync(refresh, cancellationToken);
        }

        return new DropboxAccessTokenResult(null, "missing", "none", "Dropbox access token not configured.");
    }

    public static void ResetCacheForTests()
    {
        CachedToken = null;
        CachedExpiresAt = default;
    }

    private async Task<DropboxAccessTokenResult> GetFromRefreshTokenAsync(
        ResolvedValue refresh,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(CachedToken) && CachedExpiresAt > now.AddSeconds(30))
        {
            LogAuthMode("refresh_token_cached", refresh.Source);
            return new DropboxAccessTokenResult(CachedToken, "refresh_token", refresh.Source, null);
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(CachedToken) && CachedExpiresAt > now.AddSeconds(30))
            {
                LogAuthMode("refresh_token_cached", refresh.Source);
                return new DropboxAccessTokenResult(CachedToken, "refresh_token", refresh.Source, null);
            }

            var appKey = ResolveValue(
                configuration,
                primaryKey: "Integrations:Dropbox:AppKey",
                fallbackKey: "Dropbox:AppKey",
                envPrimary: "Integrations__Dropbox__AppKey",
                envFallback: "Dropbox__AppKey");

            if (string.IsNullOrWhiteSpace(appKey.Value))
            {
                return new DropboxAccessTokenResult(null, "refresh_token", refresh.Source, "Dropbox AppKey not configured for refresh token flow.");
            }

            var appSecret = ResolveValue(
                configuration,
                primaryKey: "Integrations:Dropbox:AppSecret",
                fallbackKey: "Dropbox:AppSecret",
                envPrimary: "Integrations__Dropbox__AppSecret",
                envFallback: "Dropbox__AppSecret");

            var tokenEndpoint = configuration["Integrations:Dropbox:TokenEndpoint"]
                ?? configuration["Dropbox:TokenEndpoint"]
                ?? "https://api.dropboxapi.com/oauth2/token";

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Content = new FormUrlEncodedContent(BuildRefreshPayload(refresh.Value, appKey.Value, appSecret.Value));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new DropboxAccessTokenResult(null, "refresh_token", refresh.Source, $"Dropbox refresh failed: {(int)response.StatusCode} {json}");
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("access_token", out var tokenElement) || tokenElement.ValueKind != JsonValueKind.String)
            {
                return new DropboxAccessTokenResult(null, "refresh_token", refresh.Source, "Dropbox refresh response missing access_token.");
            }

            var accessToken = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new DropboxAccessTokenResult(null, "refresh_token", refresh.Source, "Dropbox refresh response returned empty access_token.");
            }

            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresElement)
                ? expiresElement.GetInt32()
                : 14400;
            var safeSeconds = Math.Max(30, expiresIn - 60);
            CachedToken = accessToken;
            CachedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(safeSeconds);
            LogAuthMode("refresh_token", refresh.Source);
            return new DropboxAccessTokenResult(accessToken, "refresh_token", refresh.Source, null);
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildRefreshPayload(
        string refreshToken,
        string clientId,
        string? clientSecret)
    {
        var items = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken),
            new("client_id", clientId)
        };

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            items.Add(new("client_secret", clientSecret));
        }

        return items;
    }

    private void LogAuthMode(string mode, string source)
    {
        logger?.LogInformation("MGF.Worker: Dropbox auth mode={Mode} source={Source}", mode, source);
    }

    private static ResolvedValue ResolveValue(
        IConfiguration configuration,
        string primaryKey,
        string fallbackKey,
        string envPrimary,
        string envFallback)
    {
        var value = configuration[primaryKey];
        if (!string.IsNullOrWhiteSpace(value))
        {
            var env = Environment.GetEnvironmentVariable(envPrimary);
            var source = !string.IsNullOrWhiteSpace(env) ? $"env:{envPrimary}" : "config/user-secrets";
            return new ResolvedValue(value, source);
        }

        value = configuration[fallbackKey];
        if (!string.IsNullOrWhiteSpace(value))
        {
            var env = Environment.GetEnvironmentVariable(envFallback);
            var source = !string.IsNullOrWhiteSpace(env) ? $"env:{envFallback}" : "config/user-secrets";
            return new ResolvedValue(value, source);
        }

        return new ResolvedValue(string.Empty, "none");
    }

    private readonly record struct ResolvedValue(string Value, string Source);
}
