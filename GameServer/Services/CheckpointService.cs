using GameServer.Rooms;
using Shared.Enums;
using Shared.Models;

namespace GameServer.Services;

public sealed class CheckpointService
{
    private readonly string _ownerServerId;
    private readonly Dictionary<string, long> _versions = new();
    private readonly object _syncRoot = new();

    public CheckpointService(string ownerServerId)
    {
        _ownerServerId = ownerServerId;
    }

    public RoomSnapshot CreateSnapshot(GameRoom room)
    {
        var version = NextVersion(room.RoomCode);
        return room.ToSnapshot(version) with { OwnerServerId = _ownerServerId };
    }

    public void ObserveVersion(string roomCode, long version)
    {
        lock (_syncRoot)
        {
            _versions[roomCode] = Math.Max(_versions.GetValueOrDefault(roomCode), version);
        }
    }

    public RoomSnapshot CreateEmptySnapshot(string roomCode)
    {
        return new RoomSnapshot(
            roomCode,
            GameState.Waiting,
            [],
            new Dictionary<string, int>(),
            null,
            null,
            0,
            [],
            [],
            0,
            DateTimeOffset.UtcNow)
        {
            OwnerServerId = _ownerServerId
        };
    }

    private long NextVersion(string roomCode)
    {
        lock (_syncRoot)
        {
            _versions.TryGetValue(roomCode, out var current);
            var next = current + 1;
            _versions[roomCode] = next;
            return next;
        }
    }
}
