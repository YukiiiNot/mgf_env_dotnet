namespace MGF.DevConsole.Desktop.Hosting.Connection;

public sealed class ApiConnectionStateStore : IApiConnectionStateStore
{
    private readonly object gate = new();
    private ApiConnectionState state = ApiConnectionState.Initial();

    public ApiConnectionState CurrentState
    {
        get
        {
            lock (gate)
            {
                return state;
            }
        }
    }

    public event EventHandler<ApiConnectionState>? StateChanged;

    public void UpdateState(ApiConnectionState nextState)
    {
        EventHandler<ApiConnectionState>? handler = null;
        lock (gate)
        {
            if (state == nextState)
            {
                return;
            }

            state = nextState;
            handler = StateChanged;
        }

        handler?.Invoke(this, nextState);
    }
}
