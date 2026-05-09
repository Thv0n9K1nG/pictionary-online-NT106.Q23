using System.Collections.Concurrent;
using Shared.Enums;
using Shared.Models;

namespace Gateway.Managers;

public sealed class NodeRegistry
{
    private readonly ConcurrentDictionary<string, HeartbeatInfo> _nodes = new();

    public void UpsertHeartbeat(HeartbeatInfo heartbeat)
    {
        _nodes[heartbeat.ServerId] = heartbeat;
    }

    public IReadOnlyList<HeartbeatInfo> GetHealthyNodes()
    {
        return _nodes.Values
            .Where(n => n.Status == NodeStatus.Online && n.CanAcceptRoom)
            .OrderBy(n => n.ActiveRooms)
            .ThenBy(n => n.ActivePlayers)
            .ToList();
    }

    public bool TryGetNode(string serverId, out HeartbeatInfo? heartbeat)
    {
        return _nodes.TryGetValue(serverId, out heartbeat);
    }

    public void AdjustLoad(string serverId, int roomDelta, int playerDelta)
    {
        _nodes.AddOrUpdate(
            serverId,
            _ => new HeartbeatInfo(
                ServerId: serverId,
                ActiveRooms: Math.Max(0, roomDelta),
                ActivePlayers: Math.Max(0, playerDelta),
                CpuLoad: 0,
                MemoryUsage: 0,
                CanAcceptRoom: true,
                Status: NodeStatus.Online,
                LastSeen: DateTimeOffset.UtcNow),
            (_, current) => current with
            {
                ActiveRooms = Math.Max(0, current.ActiveRooms + roomDelta),
                ActivePlayers = Math.Max(0, current.ActivePlayers + playerDelta),
                LastSeen = DateTimeOffset.UtcNow
            });
    }
}
