using Gateway.Managers;
using Shared.Models;

namespace Gateway.Services;

public sealed class LoadBalancer
{
    private readonly NodeRegistry _nodeRegistry;

    public LoadBalancer(NodeRegistry nodeRegistry)
    {
        _nodeRegistry = nodeRegistry;
    }

    public HeartbeatInfo? SelectBestServer()
    {
        return _nodeRegistry.GetHealthyNodes()
            .OrderBy(n => n.ActiveRooms * 2 + n.ActivePlayers)
            .ThenBy(n => n.CpuLoad)
            .FirstOrDefault();
    }
}
