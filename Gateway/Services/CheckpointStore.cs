using System.Collections.Concurrent;
using Shared.Models;

namespace Gateway.Services;

public sealed class CheckpointStore
{
    private readonly ConcurrentDictionary<string, RoomSnapshot> _snapshots = new();

    public void Save(RoomSnapshot snapshot)
    {
        _snapshots[snapshot.RoomCode] = snapshot;
    }

    public bool TryGet(string roomCode, out RoomSnapshot? snapshot)
    {
        return _snapshots.TryGetValue(roomCode, out snapshot);
    }
}
