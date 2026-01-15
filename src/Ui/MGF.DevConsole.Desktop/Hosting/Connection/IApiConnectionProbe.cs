namespace MGF.DevConsole.Desktop.Hosting.Connection;

public interface IApiConnectionProbe
{
    Task<ApiConnectionState> ProbeAsync(CancellationToken cancellationToken);
}
