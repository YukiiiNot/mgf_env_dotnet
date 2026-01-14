namespace MGF.DevConsole.Desktop.Hosting.Connection;

public interface IApiConnectionStateStore
{
    ApiConnectionState CurrentState { get; }

    event EventHandler<ApiConnectionState>? StateChanged;
}
