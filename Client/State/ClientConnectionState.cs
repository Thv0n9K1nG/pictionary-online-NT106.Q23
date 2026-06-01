namespace Client.State;

public enum ClientConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed
}
