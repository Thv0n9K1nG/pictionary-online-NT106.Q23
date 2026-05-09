using System.Collections.Concurrent;
using GameServer.Rooms;
using Shared.Enums;
using Shared.Models;

namespace GameServer.Managers;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

    public GameRoom CreateRoom(string hostPlayerId, string hostName, string ownerServerId)
    {
        var roomCode = GenerateRoomCode();
        var room = new GameRoom(roomCode, ownerServerId);

        room.AddPlayer(new PlayerInfo(hostPlayerId, hostName, 0, true, true));

        _rooms[roomCode] = room;
        return room;
    }

    public bool TryGetRoom(string roomCode, out GameRoom? room)
    {
        return _rooms.TryGetValue(roomCode, out room);
    }

    public GameRoom JoinRoom(string roomCode, string playerId, string playerName)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            throw new InvalidOperationException("Room not found.");
        }

        if (room.State != GameState.Waiting)
        {
            throw new InvalidOperationException("Room is not accepting players.");
        }

        room.AddPlayer(new PlayerInfo(playerId, playerName, 0, false, true));
        return room;
    }

    public int ActiveRoomCount => _rooms.Count;

    public int ActivePlayerCount => _rooms.Values.Sum(room => room.Players.Count);

    public IReadOnlyList<RoomInfo> GetWaitingRooms()
    {
        return _rooms.Values
            .Where(r => r.State == GameState.Waiting)
            .Select(r => new RoomInfo(r.RoomCode, r.HostName, r.Players.Count, 4, RoomStatus.Waiting, r.OwnerServerId))
            .ToList();
    }

    public static RoomInfo ToRoomInfo(GameRoom room)
    {
        return new RoomInfo(room.RoomCode, room.HostName, room.Players.Count, 4, RoomStatus.Waiting, room.OwnerServerId);
    }

    private static string GenerateRoomCode()
    {
        return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }
}
