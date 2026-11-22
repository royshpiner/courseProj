namespace Backend;

public sealed class ExchangeSession
{
    public SessionState State { get; private set; } = SessionState.Disconnected;

    public bool TryTransitionTo(SessionState nextState)
    {
        bool isAllowed = (State, nextState) switch
        {
            (SessionState.Disconnected, SessionState.Connecting) => true,
            (SessionState.Connecting, SessionState.LoggingIn or SessionState.Disconnected) => true,
            (SessionState.LoggingIn, SessionState.Active or SessionState.Disconnected) => true,
            (SessionState.Active, SessionState.Suspended or SessionState.LoggingOut) => true,
            (SessionState.Suspended, SessionState.Active or SessionState.LoggingOut) => true,
            (SessionState.LoggingOut, SessionState.Disconnected) => true,
            _ => false
        };

        if (!isAllowed)
        {
            return false;
        }

        State = nextState;
        return true;
    }
}