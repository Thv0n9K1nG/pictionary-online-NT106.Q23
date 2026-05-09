using System.Net.Sockets;
using System.Text.Json;
using GameServer.Managers;
using Shared.Enums;
using Shared.Models;
using Shared.Protocol;

namespace GameServer.Handlers;

public sealed class GatewayHandler
{
    private readonly string _serverId;
    private readonly RoomManager _roomManager;
    private readonly Func<NetworkStream, InternalMessageType, object, CancellationToken, Task> _sendInternalAsync;

    public GatewayHandler(
        string serverId,
        RoomManager roomManager,
        Func<NetworkStream, InternalMessageType, object, CancellationToken, Task> sendInternalAsync)
    {
        _serverId = serverId;
        _roomManager = roomManager;
        _sendInternalAsync = sendInternalAsync;
    }

    public async Task HandleInternalMessageAsync(
        JsonElement envelope,
        NetworkStream stream,
        CancellationToken cancellationToken = default)
    {
        var type = ParseInternalType(ReadProperty(envelope, "type"));
        var payload = TryGetPropertyIgnoreCase(envelope, "payload", out var payloadElement)
            ? payloadElement
            : default;

        switch (type)
        {
            case InternalMessageType.CreateRoomOnNode:
                await HandleCreateRoomOnNodeAsync(payload, stream, cancellationToken);
                break;
            case InternalMessageType.ForwardClientMessage:
                await HandleForwardClientMessageAsync(payload, stream, cancellationToken);
                break;
            default:
                Console.WriteLine($"[GameServer:{_serverId}] Internal message {type} is not handled in Stage 3.");
                break;
        }
    }

    private async Task HandleCreateRoomOnNodeAsync(JsonElement payload, NetworkStream stream, CancellationToken cancellationToken)
    {
        var requestId = ReadString(payload, "requestId");
        try
        {
            var playerId = ReadString(payload, "playerId") ?? throw new InvalidOperationException("Missing playerId.");
            var playerName = ReadString(payload, "playerName") ?? playerId;

            var room = _roomManager.CreateRoom(playerId, playerName, _serverId);
            await SendRoomResponseAsync(requestId, room, stream, cancellationToken);

            Console.WriteLine($"[GameServer:{_serverId}] Created room {room.RoomCode}.");
        }
        catch (Exception ex)
        {
            await SendFailureAsync(requestId, ex.Message, stream, cancellationToken);
        }
    }

    private async Task HandleForwardClientMessageAsync(JsonElement payload, NetworkStream stream, CancellationToken cancellationToken)
    {
        var requestId = ReadString(payload, "requestId");
        try
        {
            var roomCode = ReadString(payload, "roomCode")?.Trim().ToUpperInvariant()
                ?? throw new InvalidOperationException("Missing roomCode.");
            var playerId = ReadString(payload, "playerId") ?? throw new InvalidOperationException("Missing playerId.");
            var playerName = ReadString(payload, "playerName") ?? playerId;

            var room = _roomManager.JoinRoom(roomCode, playerId, playerName);
            await SendRoomResponseAsync(requestId, room, stream, cancellationToken);

            Console.WriteLine($"[GameServer:{_serverId}] Player {playerId} joined room {room.RoomCode}.");
        }
        catch (Exception ex)
        {
            await SendFailureAsync(requestId, ex.Message, stream, cancellationToken);
        }
    }

    private Task SendRoomResponseAsync(
        string? requestId,
        GameServer.Rooms.GameRoom room,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var roomInfo = RoomManager.ToRoomInfo(room);
        var payload = new
        {
            requestId,
            success = true,
            roomCode = room.RoomCode,
            roomInfo,
            players = room.Players
        };

        return _sendInternalAsync(stream, InternalMessageType.ServerEvent, payload, cancellationToken);
    }

    private Task SendFailureAsync(string? requestId, string error, NetworkStream stream, CancellationToken cancellationToken)
    {
        var payload = new
        {
            requestId,
            success = false,
            error
        };

        return _sendInternalAsync(stream, InternalMessageType.ServerEvent, payload, cancellationToken);
    }

    private static JsonElement ReadProperty(JsonElement element, string name)
    {
        return TryGetPropertyIgnoreCase(element, name, out var value) ? value : default;
    }

    private static string? ReadString(JsonElement payload, string name)
    {
        if (TryGetPropertyIgnoreCase(payload, name, out var value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        return null;
    }

    private static InternalMessageType ParseInternalType(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return Enum.IsDefined(typeof(InternalMessageType), number)
                ? (InternalMessageType)number
                : InternalMessageType.Unknown;
        }

        var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return InternalMessageType.Unknown;
        }

        if (Enum.TryParse<InternalMessageType>(raw, ignoreCase: true, out var direct))
        {
            return direct;
        }

        var normalized = NormalizeToken(raw);
        foreach (var value in Enum.GetValues<InternalMessageType>())
        {
            if (NormalizeToken(value.ToString()) == normalized)
            {
                return value;
            }
        }

        return InternalMessageType.Unknown;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string NormalizeToken(string value)
    {
        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }
}
