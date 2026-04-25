using Shared.Enums;
using Shared.Models;

namespace GameServer.Services;

public sealed class CheckpointService
{
    private readonly string _ownerServerId;

    public CheckpointService(string ownerServerId)
    {
        _ownerServerId = ownerServerId;
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
            DateTimeOffset.UtcNow
        );
    }
}
