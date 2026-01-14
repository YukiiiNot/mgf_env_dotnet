namespace MGF.DevConsole.Desktop.Api;

public interface IMetaApiClient
{
    TimeSpan Timeout { get; }

    Task<MetaApiClient.MetaDto> GetMetaAsync(CancellationToken cancellationToken);
}
