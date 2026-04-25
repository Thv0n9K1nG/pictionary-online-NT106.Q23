using Shared.Models;

namespace GameServer.Handlers;

public sealed class GatewayHandler
{
    public Task HandleAsync(GameMessage message, CancellationToken cancellationToken = default)
    {
        // TODO: Handle CREATE_ROOM_ON_NODE, FORWARD_CLIENT_MESSAGE, RESTORE_ROOM_FROM_CHECKPOINT.
        return Task.CompletedTask;
    }
}
