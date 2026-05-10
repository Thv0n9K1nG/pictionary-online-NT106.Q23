using Gateway.Managers;
using Shared.Models;
using Shared.Protocol;

namespace Gateway.Services;

public sealed class ProxyRouter
{
    private readonly RoomDirectory _roomDirectory;
    private readonly SessionDirectory _sessionDirectory;
    private readonly GameServerConnectionDirectory? _gameServerConnections;

    public ProxyRouter(RoomDirectory roomDirectory, SessionDirectory sessionDirectory)
    {
        _roomDirectory = roomDirectory;
        _sessionDirectory = sessionDirectory;
    }

    public ProxyRouter(
        RoomDirectory roomDirectory,
        SessionDirectory sessionDirectory,
        GameServerConnectionDirectory gameServerConnections)
        : this(roomDirectory, sessionDirectory)
    {
        _gameServerConnections = gameServerConnections;
    }

    public async Task RouteClientMessageAsync(
        string roomCode,
        string sessionId,
        GameMessage innerMessage,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionDirectory.TryGet(sessionId, out var session) || session is null)
        {
            throw new InvalidOperationException("Invalid session.");
        }

        if (!_roomDirectory.TryGetOwner(roomCode, out var ownerServerId) || string.IsNullOrWhiteSpace(ownerServerId))
        {
            throw new InvalidOperationException("Room owner not found.");
        }

        if (_gameServerConnections is null ||
            !_gameServerConnections.TryGet(ownerServerId, out var gameServer) ||
            gameServer is null)
        {
            throw new InvalidOperationException("Room owner GameServer is not connected.");
        }

        var response = await gameServer.SendInternalRequestAsync(
            InternalMessageType.ForwardClientMessage,
            new
            {
                roomCode,
                sessionId,
                playerId = session.PlayerId,
                innerMessage
            },
            cancellationToken);

        if (response.TryGetProperty("success", out var successElement) &&
            successElement.ValueKind == System.Text.Json.JsonValueKind.False)
        {
            var error = response.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString()
                : "GameServer rejected the message.";
            throw new InvalidOperationException(error);
        }
    }
}
