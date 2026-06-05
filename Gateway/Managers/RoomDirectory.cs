using System.Collections.Concurrent;
using Shared.Enums;
using Shared.Models;

namespace Gateway.Managers;

public sealed class RoomDirectory
{
    private readonly ConcurrentDictionary<string, string> _roomOwners = new();
    private readonly ConcurrentDictionary<string, RoomInfo> _rooms = new();

    public void SetOwner(string roomCode, string ownerServerId)
    {
        _roomOwners[roomCode] = ownerServerId;
    }

    public void UpsertRoom(RoomInfo roomInfo)
    {
        if (!string.IsNullOrWhiteSpace(roomInfo.OwnerServerId))
        {
            SetOwner(roomInfo.RoomCode, roomInfo.OwnerServerId);
        }

        _rooms[roomInfo.RoomCode] = roomInfo;
    }

    public bool TryGetOwner(string roomCode, out string? ownerServerId)
    {
        return _roomOwners.TryGetValue(roomCode, out ownerServerId);
    }

    public bool TryGetRoom(string roomCode, out RoomInfo? roomInfo)
    {
        return _rooms.TryGetValue(roomCode, out roomInfo);
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        return _roomOwners;
    }

    public IReadOnlyList<RoomInfo> GetWaitingRooms()
    {
        return _rooms.Values
            .Where(room => room.Status == RoomStatus.Waiting)
            .OrderBy(room => room.RoomCode)
            .ToList();
    }

    public void Remove(string roomCode)
    {
        _rooms.TryRemove(roomCode, out _);
        _roomOwners.TryRemove(roomCode, out _);
    }
}
