namespace MGF.DevConsole.Desktop.Hosting.Connection;

using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using MGF.DevConsole.Desktop.Api;

public sealed class ApiConnectionProbe : IApiConnectionProbe
{
    private const string MessageMissingBaseUrl = "Api:BaseUrl is not configured.";
    private const string MessageInvalidBaseUrl = "Api:BaseUrl is not a valid absolute URL.";
    private const string MessageMissingApiKey = "Security:ApiKey is not configured.";
    private const string MessageUnauthorized = "Unauthorized (X-MGF-API-KEY rejected).";
    private const string MessageOffline = "API not reachable.";
    private const string MessageTimeout = "API request timed out.";
    private const string MessageInvalidResponse = "API response invalid.";
    private const string MessageProbeFailed = "API probe failed.";

    private readonly IConfiguration config;
    private readonly IMetaApiClient metaClient;

    public ApiConnectionProbe(IConfiguration config, IMetaApiClient metaClient)
    {
        this.config = config;
        this.metaClient = metaClient;
    }

    public async Task<ApiConnectionState> ProbeAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var baseUrl = config["Api:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new ApiConnectionState(ApiConnectionStatus.Misconfigured, MessageMissingBaseUrl, now, null, null);
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            return new ApiConnectionState(ApiConnectionStatus.Misconfigured, MessageInvalidBaseUrl, now, null, null);
        }

        var apiKey = config["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ApiConnectionState(ApiConnectionStatus.Misconfigured, MessageMissingApiKey, now, null, null);
        }

        try
        {
            var meta = await metaClient.GetMetaAsync(cancellationToken);
            return new ApiConnectionState(
                ApiConnectionStatus.Connected,
                "Connected",
                now,
                now,
                meta.MgfEnv);
        }
        catch (MetaApiException ex) when (ex.Failure == MetaApiFailure.Unauthorized)
        {
            return new ApiConnectionState(ApiConnectionStatus.Unauthorized, MessageUnauthorized, now, null, null);
        }
        catch (MetaApiException ex) when (ex.Failure == MetaApiFailure.HttpError)
        {
            var code = ex.StatusCode.HasValue ? ((int)ex.StatusCode.Value).ToString() : "unknown";
            var message = $"API responded with HTTP {code}.";
            return new ApiConnectionState(ApiConnectionStatus.Degraded, message, now, null, null);
        }
        catch (MetaApiException ex) when (ex.Failure == MetaApiFailure.InvalidResponse)
        {
            return new ApiConnectionState(ApiConnectionStatus.Degraded, MessageInvalidResponse, now, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return new ApiConnectionState(ApiConnectionStatus.Offline, MessageTimeout, now, null, null);
        }
        catch (HttpRequestException)
        {
            return new ApiConnectionState(ApiConnectionStatus.Offline, MessageOffline, now, null, null);
        }
        catch (Exception)
        {
            return new ApiConnectionState(ApiConnectionStatus.Degraded, MessageProbeFailed, now, null, null);
        }
    }
}
