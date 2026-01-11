namespace MGF.DevConsole.Desktop.Api;

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

public sealed class MetaApiClient : IMetaApiClient
{
    private readonly HttpClient httpClient;

    public MetaApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public sealed record MetaDto(
        string EnvironmentName,
        string MgfEnv,
        string MgfDbMode,
        string ApplicationName,
        DateTimeOffset ServerUtc
    );

    public TimeSpan Timeout => httpClient.Timeout;

    public async Task<MetaDto> GetMetaAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("api/meta", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new MetaApiException(MetaApiFailure.Unauthorized, "Unauthorized (X-MGF-API-KEY rejected).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new MetaApiException(
                MetaApiFailure.HttpError,
                $"Unexpected status code {(int)response.StatusCode}.",
                response.StatusCode);
        }

        var meta = await response.Content.ReadFromJsonAsync<MetaDto>(cancellationToken: cancellationToken);
        if (meta is null)
        {
            throw new MetaApiException(MetaApiFailure.InvalidResponse, "Missing or invalid meta response.");
        }

        return meta;
    }
}

public sealed class MetaApiException : Exception
{
    public MetaApiException(MetaApiFailure failure, string message, HttpStatusCode? statusCode = null)
        : base(message)
    {
        Failure = failure;
        StatusCode = statusCode;
    }

    public MetaApiFailure Failure { get; }

    public HttpStatusCode? StatusCode { get; }
}

public enum MetaApiFailure
{
    Unauthorized,
    HttpError,
    InvalidResponse
}
