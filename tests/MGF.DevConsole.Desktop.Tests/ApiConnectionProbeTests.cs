using System.Net;
using Microsoft.Extensions.Configuration;
using MGF.DevConsole.Desktop.Api;
using MGF.DevConsole.Desktop.Hosting.Connection;

public sealed class ApiConnectionProbeTests
{
    [Fact]
    public async Task ProbeAsync_ReturnsMisconfigured_WhenBaseUrlMissing()
    {
        var probe = new ApiConnectionProbe(BuildConfig(), new FakeMetaApiClient());

        var state = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ApiConnectionStatus.Misconfigured, state.Status);
        Assert.Equal("Api:BaseUrl is not configured.", state.Message);
    }

    [Fact]
    public async Task ProbeAsync_ReturnsMisconfigured_WhenBaseUrlInvalid()
    {
        var probe = new ApiConnectionProbe(
            BuildConfig(("Api:BaseUrl", "not-a-url")),
            new FakeMetaApiClient());

        var state = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ApiConnectionStatus.Misconfigured, state.Status);
        Assert.Equal("Api:BaseUrl is not a valid absolute URL.", state.Message);
    }

    [Fact]
    public async Task ProbeAsync_ReturnsMisconfigured_WhenApiKeyMissing()
    {
        var probe = new ApiConnectionProbe(
            BuildConfig(("Api:BaseUrl", "http://localhost:5000")),
            new FakeMetaApiClient());

        var state = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ApiConnectionStatus.Misconfigured, state.Status);
        Assert.Equal("Security:ApiKey is not configured.", state.Message);
    }

    [Fact]
    public async Task ProbeAsync_MapsUnauthorized()
    {
        var probe = new ApiConnectionProbe(
            BuildConfig(("Api:BaseUrl", "http://localhost:5000"), ("Security:ApiKey", "key")),
            new FakeMetaApiClient
            {
                Handler = _ => throw new MetaApiException(MetaApiFailure.Unauthorized, "Unauthorized (X-MGF-API-KEY rejected).")
            });

        var state = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ApiConnectionStatus.Unauthorized, state.Status);
        Assert.Equal("Unauthorized (X-MGF-API-KEY rejected).", state.Message);
    }

    [Fact]
    public async Task ProbeAsync_MapsHttpErrorToDegraded()
    {
        var probe = new ApiConnectionProbe(
            BuildConfig(("Api:BaseUrl", "http://localhost:5000"), ("Security:ApiKey", "key")),
            new FakeMetaApiClient
            {
                Handler = _ => throw new MetaApiException(
                    MetaApiFailure.HttpError,
                    "Unexpected status code 503.",
                    HttpStatusCode.ServiceUnavailable)
            });

        var state = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ApiConnectionStatus.Degraded, state.Status);
        Assert.Equal("API responded with HTTP 503.", state.Message);
    }

    [Fact]
    public async Task ProbeAsync_MapsTimeoutToOffline()
    {
        var probe = new ApiConnectionProbe(
            BuildConfig(("Api:BaseUrl", "http://localhost:5000"), ("Security:ApiKey", "key")),
            new FakeMetaApiClient
            {
                Handler = _ => throw new TaskCanceledException("timeout")
            });

        var state = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ApiConnectionStatus.Offline, state.Status);
        Assert.Equal("API request timed out.", state.Message);
    }

    [Fact]
    public async Task ProbeAsync_MapsInvalidResponseToDegraded()
    {
        var probe = new ApiConnectionProbe(
            BuildConfig(("Api:BaseUrl", "http://localhost:5000"), ("Security:ApiKey", "key")),
            new FakeMetaApiClient
            {
                Handler = _ => throw new MetaApiException(MetaApiFailure.InvalidResponse, "Missing or invalid meta response.")
            });

        var state = await probe.ProbeAsync(CancellationToken.None);

        Assert.Equal(ApiConnectionStatus.Degraded, state.Status);
        Assert.Equal("API response invalid.", state.Message);
    }

    private static IConfiguration BuildConfig(params (string Key, string Value)[] values)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            data[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    private sealed class FakeMetaApiClient : IMetaApiClient
    {
        public Func<CancellationToken, Task<MetaApiClient.MetaDto>> Handler { get; init; } =
            _ => Task.FromResult(new MetaApiClient.MetaDto("Development", "Dev", "Auto", "MGF.Api", DateTimeOffset.UtcNow));

        public TimeSpan Timeout => TimeSpan.FromSeconds(3);

        public Task<MetaApiClient.MetaDto> GetMetaAsync(CancellationToken cancellationToken)
        {
            return Handler(cancellationToken);
        }
    }
}
