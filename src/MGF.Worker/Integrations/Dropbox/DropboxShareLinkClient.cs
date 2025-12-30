namespace MGF.Worker.Integrations.Dropbox;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public interface IDropboxShareLinkClient
{
    Task ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken);

    Task<DropboxShareLinkResult> GetOrCreateSharedLinkAsync(
        string accessToken,
        string dropboxPath,
        CancellationToken cancellationToken);
}

public sealed record DropboxShareLinkResult(string Url, string? Id, bool IsNew);

internal sealed class DropboxShareLinkClient : IDropboxShareLinkClient
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;

    public DropboxShareLinkClient(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        var url = GetApiBaseUrl() + "/users/get_current_account";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = null;

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Dropbox token validation failed: {(int)response.StatusCode} {body}");
    }

    public async Task<DropboxShareLinkResult> GetOrCreateSharedLinkAsync(
        string accessToken,
        string dropboxPath,
        CancellationToken cancellationToken)
    {
        var existing = await ListSharedLinkAsync(accessToken, dropboxPath, cancellationToken);
        if (existing is not null)
        {
            return existing with { IsNew = false };
        }

        var created = await CreateSharedLinkAsync(accessToken, dropboxPath, cancellationToken);
        return created with { IsNew = true };
    }

    private async Task<DropboxShareLinkResult?> ListSharedLinkAsync(
        string accessToken,
        string dropboxPath,
        CancellationToken cancellationToken)
    {
        var url = GetApiBaseUrl() + "/sharing/list_shared_links";
        var payload = JsonSerializer.Serialize(new
        {
            path = dropboxPath,
            direct_only = true
        });

        using var response = await SendAsync(accessToken, url, payload, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var link in links.EnumerateArray())
        {
            var urlValue = GetString(link, "url");
            if (string.IsNullOrWhiteSpace(urlValue))
            {
                continue;
            }

            var idValue = GetString(link, "id");
            return new DropboxShareLinkResult(urlValue, idValue, IsNew: false);
        }

        return null;
    }

    private async Task<DropboxShareLinkResult> CreateSharedLinkAsync(
        string accessToken,
        string dropboxPath,
        CancellationToken cancellationToken)
    {
        var url = GetApiBaseUrl() + "/sharing/create_shared_link_with_settings";
        var payload = JsonSerializer.Serialize(new
        {
            path = dropboxPath,
            settings = new
            {
                requested_visibility = "public"
            }
        });

        using var response = await SendAsync(accessToken, url, payload, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var urlValue = GetString(root, "url");
        if (string.IsNullOrWhiteSpace(urlValue))
        {
            throw new InvalidOperationException("Dropbox response missing share url.");
        }

        var idValue = GetString(root, "id");
        return new DropboxShareLinkResult(urlValue, idValue, IsNew: true);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string accessToken,
        string url,
        string payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Dropbox API error {(int)response.StatusCode}: {body}");
    }

    private string GetApiBaseUrl()
    {
        var configured = configuration["Integrations:Dropbox:ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return "https://api.dropboxapi.com/2";
        }

        return configured.Trim().TrimEnd('/');
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }
}
