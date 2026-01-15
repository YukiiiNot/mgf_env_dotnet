namespace MGF.DevConsole.Desktop.Hosting.Connection;

public interface IApiConnectionMonitor
{
    void Start();

    void Stop();

    void RequestProbe();
}
