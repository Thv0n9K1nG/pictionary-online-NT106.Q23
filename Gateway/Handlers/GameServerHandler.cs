using Shared.Models;

namespace Gateway.Handlers;

public sealed class GameServerHandler
{
    public string? ServerId { get; private set; }

    public Task SendAsync(GameMessage message, CancellationToken cancellationToken = default)
    {
        // TODO: Write internal message to GameServer TCP stream.
        return Task.CompletedTask;
    }

    public Task HandleAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Handle NODE_REGISTER, NODE_HEARTBEAT, SERVER_EVENT, ROOM_CHECKPOINT.
        return Task.CompletedTask;
    }
}
