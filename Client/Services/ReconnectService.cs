namespace Client.Services;

public sealed class ReconnectService
{
    private readonly SocketService _socketService;

    public ReconnectService(SocketService socketService)
    {
        _socketService = socketService;
    }

    public Task TryReconnectAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // TODO: Reconnect to Gateway and send RECONNECT(sessionId).
        return Task.CompletedTask;
    }
}
