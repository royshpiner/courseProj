namespace Backend;

public enum SessionState
{
    Disconnected,
    Connecting,
    LoggingIn,
    Active,
    Suspended,
    LoggingOut
}