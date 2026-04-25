using Gateway.Managers;

namespace Gateway.Services;

public sealed class RecoveryCoordinator
{
    private readonly NodeRegistry _nodeRegistry;
    private readonly RoomDirectory _roomDirectory;
    private readonly CheckpointStore _checkpointStore;

    public RecoveryCoordinator(NodeRegistry nodeRegistry, RoomDirectory roomDirectory, CheckpointStore checkpointStore)
    {
        _nodeRegistry = nodeRegistry;
        _roomDirectory = roomDirectory;
        _checkpointStore = checkpointStore;
    }

    public Task RecoverRoomsOwnedByAsync(string failedServerId, CancellationToken cancellationToken = default)
    {
        // TODO: Find affected rooms, select new owner, restore from checkpoint.
        return Task.CompletedTask;
    }
}
