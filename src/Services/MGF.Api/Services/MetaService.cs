namespace MGF.Api.Services;

using Microsoft.Extensions.Hosting;

public sealed class MetaService
{
    private readonly IHostEnvironment hostEnvironment;

    public MetaService(IHostEnvironment hostEnvironment)
    {
        this.hostEnvironment = hostEnvironment;
    }

    public sealed record MetaDto(
        string EnvironmentName,
        string MgfEnv,
        string MgfDbMode,
        string ApplicationName,
        DateTimeOffset ServerUtc
    );

    public MetaDto GetMeta()
    {
        var mgfEnv = Environment.GetEnvironmentVariable("MGF_ENV") ?? string.Empty;
        var mgfDbMode = Environment.GetEnvironmentVariable("MGF_DB_MODE") ?? string.Empty;
        var applicationName = hostEnvironment.ApplicationName ?? string.Empty;

        return new MetaDto(
            EnvironmentName: hostEnvironment.EnvironmentName,
            MgfEnv: mgfEnv,
            MgfDbMode: mgfDbMode,
            ApplicationName: applicationName,
            ServerUtc: DateTimeOffset.UtcNow
        );
    }
}
