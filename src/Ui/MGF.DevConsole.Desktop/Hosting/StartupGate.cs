namespace MGF.DevConsole.Desktop.Hosting;

using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MGF.DevConsole.Desktop.Api;

public sealed class StartupGate
{
    private readonly IConfiguration config;
    private readonly MetaApiClient metaClient;
    private readonly IHostEnvironment environment;

    public StartupGate(IConfiguration config, MetaApiClient metaClient, IHostEnvironment environment)
    {
        this.config = config;
        this.metaClient = metaClient;
        this.environment = environment;
    }

    public async Task EnsureEnvironmentMatchesAsync(CancellationToken cancellationToken)
    {
        var expectedEnv = Environment.GetEnvironmentVariable("MGF_ENV");
        if (string.IsNullOrWhiteSpace(expectedEnv))
        {
            throw new StartupGateException("MGF_ENV is not set. Set MGF_ENV before starting DevConsole.");
        }

        var baseUrl = config["Api:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new StartupGateException("Api:BaseUrl is not configured. Set the API base URL before starting DevConsole.");
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            throw new StartupGateException($"Api:BaseUrl is not a valid absolute URL: {baseUrl}");
        }

        var apiKey = config["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var envNote = string.Equals(environment.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase)
                ? " DOTNET_ENVIRONMENT is not set to Development, so appsettings.Development.json will not load."
                : string.Empty;
            throw new StartupGateException(
                "Security:ApiKey is not configured. Copy config/appsettings.Development.sample.json to "
                + "config/appsettings.Development.json and set Security:ApiKey, or set SECURITY__APIKEY. "
                + "The same key must be configured for MGF.Api because it enforces X-MGF-API-KEY."
                + envNote);
        }

        MetaApiClient.MetaDto meta;
        try
        {
            meta = await metaClient.GetMetaAsync(cancellationToken);
        }
        catch (MetaApiException ex) when (ex.Failure == MetaApiFailure.Unauthorized)
        {
            throw new StartupGateException("DevConsole API key was rejected (401). Check Security:ApiKey.");
        }
        catch (TaskCanceledException)
        {
            var timeoutSeconds = metaClient.Timeout.TotalSeconds;
            var timeoutLabel = timeoutSeconds > 0 ? $"{timeoutSeconds:0} seconds" : "the configured timeout";
            throw new StartupGateException($"DevConsole could not reach the API within {timeoutLabel}.");
        }
        catch (HttpRequestException ex)
        {
            throw new StartupGateException($"DevConsole could not reach the API at {baseUrl}. {ex.Message}");
        }
        catch (MetaApiException ex)
        {
            throw new StartupGateException($"DevConsole received an invalid meta response. {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(meta.MgfEnv))
        {
            throw new StartupGateException("API returned an empty MGF_ENV. Refusing to start.");
        }

        if (!string.Equals(meta.MgfEnv, expectedEnv, StringComparison.OrdinalIgnoreCase))
        {
            throw new StartupGateException(
                $"Environment mismatch. Local MGF_ENV={expectedEnv}, API MGF_ENV={meta.MgfEnv}.");
        }
    }
}

public sealed class StartupGateException : Exception
{
    public StartupGateException(string message)
        : base(message)
    {
    }
}
