using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using MGF.Worker.Integrations.Dropbox;

namespace MGF.Worker.Tests;

public sealed class DropboxAccessTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_UsesRefreshTokenAndCaches()
    {
        DropboxAccessTokenProvider.ResetCacheForTests();

        var settings = new Dictionary<string, string?>
        {
            ["Integrations:Dropbox:RefreshToken"] = "refresh-token",
            ["Integrations:Dropbox:AppKey"] = "app-key",
            ["Integrations:Dropbox:AppSecret"] = "app-secret",
            ["Integrations:Dropbox:TokenEndpoint"] = "https://dropbox.test/token"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"token-123\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
            });
        var httpClient = new HttpClient(handler);

        var provider = new DropboxAccessTokenProvider(httpClient, config);
        var first = await provider.GetAccessTokenAsync(CancellationToken.None);
        var second = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("token-123", first.AccessToken);
        Assert.Equal("refresh_token", first.AuthMode);
        Assert.Equal("token-123", second.AccessToken);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_FallsBackToAccessToken()
    {
        DropboxAccessTokenProvider.ResetCacheForTests();

        var settings = new Dictionary<string, string?>
        {
            ["Integrations:Dropbox:AccessToken"] = "access-123"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var httpClient = new HttpClient(handler);

        var provider = new DropboxAccessTokenProvider(httpClient, config);
        var result = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("access-123", result.AccessToken);
        Assert.Equal("access_token", result.AuthMode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_PrefersAccessTokenWhenPresent()
    {
        DropboxAccessTokenProvider.ResetCacheForTests();

        var settings = new Dictionary<string, string?>
        {
            ["Integrations:Dropbox:AccessToken"] = "access-123",
            ["Integrations:Dropbox:RefreshToken"] = "refresh-token",
            ["Integrations:Dropbox:AppKey"] = "app-key",
            ["Integrations:Dropbox:AppSecret"] = "app-secret",
            ["Integrations:Dropbox:TokenEndpoint"] = "https://dropbox.test/token"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"token-456\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
            });
        var httpClient = new HttpClient(handler);

        var provider = new DropboxAccessTokenProvider(httpClient, config);
        var result = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("access-123", result.AccessToken);
        Assert.Equal("access_token", result.AuthMode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsErrorWhenRefreshMissingAppKey()
    {
        DropboxAccessTokenProvider.ResetCacheForTests();

        var settings = new Dictionary<string, string?>
        {
            ["Integrations:Dropbox:RefreshToken"] = "refresh-token",
            ["Integrations:Dropbox:TokenEndpoint"] = "https://dropbox.test/token"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler);

        var provider = new DropboxAccessTokenProvider(httpClient, config);
        var result = await provider.GetAccessTokenAsync(CancellationToken.None);

        Assert.True(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Contains("AppKey", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(handler(request));
        }
    }
}
