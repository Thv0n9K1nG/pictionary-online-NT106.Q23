using Gateway.Managers;
using Shared.Models;

namespace Gateway.Services;

public sealed class ProxyRouter
{
    private readonly RoomDirectory _roomDirectory;
    private readonly SessionDirectory _sessionDirectory;

    public ProxyRouter(RoomDirectory roomDirectory, SessionDirectory sessionDirectory)
    {
        _roomDirectory = roomDirectory;
        _sessionDirectory = sessionDirectory;
    }

    public Task RouteClientMessageAsync(string roomCode, string sessionId, GameMessage innerMessage, CancellationToken cancellationToken = default)
    {
        if (!_sessionDirectory.TryGet(sessionId, out _))
        {
            throw new InvalidOperationException("Invalid session.");
        }

        if (!_roomDirectory.TryGetOwner(roomCode, out var ownerServerId) || string.IsNullOrWhiteSpace(ownerServerId))
        {
            throw new InvalidOperationException("Room owner not found.");
        }

        // TODO: Find GameServerHandler by ownerServerId and forward FORWARD_CLIENT_MESSAGE.
        return Task.CompletedTask;
    }
}
