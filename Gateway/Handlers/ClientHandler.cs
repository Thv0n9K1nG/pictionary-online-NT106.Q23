using Shared.Models;

namespace Gateway.Handlers;

public sealed class ClientHandler
{
    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

    public Task SendAsync(GameMessage message, CancellationToken cancellationToken = default)
    {
        // TODO: Write serialized GameMessage to TLS stream.
        return Task.CompletedTask;
    }

    public Task HandleAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Read client messages, validate session, then route through ProxyRouter.
        return Task.CompletedTask;
    }
}
