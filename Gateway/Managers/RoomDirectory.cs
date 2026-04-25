using System.Collections.Concurrent;

namespace Gateway.Managers;

public sealed class RoomDirectory
{
    private readonly ConcurrentDictionary<string, string> _roomOwners = new();

    public void SetOwner(string roomCode, string ownerServerId)
    {
        _roomOwners[roomCode] = ownerServerId;
    }

    public bool TryGetOwner(string roomCode, out string? ownerServerId)
    {
        return _roomOwners.TryGetValue(roomCode, out ownerServerId);
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        return _roomOwners;
    }
}
