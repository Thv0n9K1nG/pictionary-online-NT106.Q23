using Shared.Enums;
using Shared.Models;

namespace GameServer.Rooms;

public sealed class GameRoom
{
    private readonly List<PlayerInfo> _players = new();

    public GameRoom(string roomCode, string ownerServerId)
    {
        RoomCode = roomCode;
        OwnerServerId = ownerServerId;
    }

    public string RoomCode { get; }
    public string OwnerServerId { get; private set; }
    public GameState State { get; private set; } = GameState.Waiting;
    public IReadOnlyList<PlayerInfo> Players => _players;
    public string HostName => _players.FirstOrDefault(p => p.IsHost)?.DisplayName ?? "Unknown";

    public void AddPlayer(PlayerInfo player)
    {
        if (_players.Any(existing => existing.PlayerId == player.PlayerId))
        {
            return;
        }

        if (_players.Count >= 4)
        {
            throw new InvalidOperationException("Room is full.");
        }

        _players.Add(player);
    }

    public void RestoreOwner(string ownerServerId)
    {
        OwnerServerId = ownerServerId;
    }
}
