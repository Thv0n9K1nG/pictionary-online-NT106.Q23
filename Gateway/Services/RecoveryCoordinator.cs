using System.Text.Json;
using System.Text.Json.Serialization;
using Gateway.Handlers;
using Gateway.Managers;
using Shared.Enums;
using Shared.Models;
using Shared.Protocol;

namespace Gateway.Services;

public sealed class RecoveryCoordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly NodeRegistry _nodeRegistry;
    private readonly RoomDirectory _roomDirectory;
    private readonly CheckpointStore _checkpointStore;
    private readonly GameServerConnectionDirectory _gameServerConnections;
    private readonly ClientConnectionDirectory _clientConnections;

    public RecoveryCoordinator(
        NodeRegistry nodeRegistry,
        RoomDirectory roomDirectory,
        CheckpointStore checkpointStore,
        GameServerConnectionDirectory gameServerConnections,
        ClientConnectionDirectory clientConnections)
    {
        _nodeRegistry = nodeRegistry;
        _roomDirectory = roomDirectory;
        _checkpointStore = checkpointStore;
        _gameServerConnections = gameServerConnections;
        _clientConnections = clientConnections;
    }

    public async Task RecoverRoomsOwnedByAsync(string failedServerId, CancellationToken cancellationToken = default)
    {
        var affectedRooms = _roomDirectory.GetAll()
            .Where(room => string.Equals(room.Value, failedServerId, StringComparison.OrdinalIgnoreCase))
            .Select(room => room.Key)
            .ToList();

        foreach (var roomCode in affectedRooms)
        {
            if (!_checkpointStore.TryGet(roomCode, out var snapshot) || snapshot is null)
            {
                Console.WriteLine($"[Gateway][Recovery] No checkpoint for room {roomCode}; cannot recover.");
                continue;
            }

            var target = SelectRecoveryTarget(failedServerId);
            if (target is null)
            {
                Console.WriteLine($"[Gateway][Recovery] No healthy GameServer available for room {roomCode}.");
                continue;
            }

            try
            {
                var response = await target.SendInternalRequestAsync(
                    InternalMessageType.RestoreRoomFromCheckpoint,
                    snapshot,
                    cancellationToken);

                if (!ReadBool(response, "success"))
                {
                    Console.WriteLine($"[Gateway][Recovery] Restore rejected for room {roomCode}: {ReadString(response, "error")}");
                    continue;
                }

                var ownerServerId = ReadString(response, "ownerServerId") ?? target.ServerId;
                if (string.IsNullOrWhiteSpace(ownerServerId))
                {
                    continue;
                }

                var roomInfo = ReadRoomInfo(response) ?? new RoomInfo(
                    roomCode,
                    snapshot.Players.FirstOrDefault(player => player.IsHost)?.DisplayName ?? "Unknown",
                    snapshot.Players.Count,
                    4,
                    ToRoomStatus(snapshot.GameState),
                    ownerServerId);

                // Stage 6 - A: move the room owner only after the new GameServer confirms restore.
                _roomDirectory.UpsertRoom(roomInfo with { OwnerServerId = ownerServerId });
                await BroadcastRecoveredAsync(roomCode, ownerServerId, roomInfo with { OwnerServerId = ownerServerId }, cancellationToken);
                Console.WriteLine($"[Gateway][Recovery] Room {roomCode} moved from {failedServerId} to {ownerServerId}.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[Gateway][Recovery] Failed to recover room {roomCode}: {ex.Message}");
            }
        }
    }

    private GameServerHandler? SelectRecoveryTarget(string failedServerId)
    {
        foreach (var node in _nodeRegistry.GetHealthyNodes())
        {
            if (string.Equals(node.ServerId, failedServerId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_gameServerConnections.TryGet(node.ServerId, out var handler) && handler is not null)
            {
                return handler;
            }
        }

        return null;
    }

    private async Task BroadcastRecoveredAsync(
        string roomCode,
        string ownerServerId,
        RoomInfo roomInfo,
        CancellationToken cancellationToken)
    {
        var message = new GameMessage
        {
            Type = MessageType.RoomRecovered,
            Payload = new { roomCode, ownerServerId, roomInfo }
        };

        foreach (var client in _clientConnections.GetRoomClients(roomCode))
        {
            await client.SendAsync(message, cancellationToken);
        }
    }

    private static RoomStatus ToRoomStatus(GameState state)
    {
        return state switch
        {
            GameState.Waiting => RoomStatus.Waiting,
            GameState.SelectingWord or GameState.Drawing or GameState.RoundEnd => RoomStatus.Playing,
            GameState.GameOver => RoomStatus.Ended,
            _ => RoomStatus.Waiting
        };
    }

    private static RoomInfo? ReadRoomInfo(JsonElement response)
    {
        if (TryGetPropertyIgnoreCase(response, "roomInfo", out var roomInfoElement))
        {
            return roomInfoElement.Deserialize<RoomInfo>(JsonOptions);
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return TryGetPropertyIgnoreCase(element, name, out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString()
            : null;
    }

    private static bool ReadBool(JsonElement element, string name)
    {
        return TryGetPropertyIgnoreCase(element, name, out var property) &&
               (property.ValueKind == JsonValueKind.True ||
                property.ValueKind == JsonValueKind.String && bool.TryParse(property.GetString(), out var parsed) && parsed);
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
}
