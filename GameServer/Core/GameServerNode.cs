using GameServer.Managers;
using GameServer.Services;

namespace GameServer.Core;

public sealed class GameServerNode
{
    private readonly string _serverId;
    private readonly string _gatewayHost;
    private readonly int _gatewayPort;
    private readonly RoomManager _roomManager = new();

    public GameServerNode(string serverId, string gatewayHost, int gatewayPort)
    {
        _serverId = serverId;
        _gatewayHost = gatewayHost;
        _gatewayPort = gatewayPort;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[GameServer:{_serverId}] Starting");
        Console.WriteLine($"[GameServer:{_serverId}] Gateway target: {_gatewayHost}:{_gatewayPort}");
        Console.WriteLine($"[GameServer:{_serverId}] Baseline mode: internal socket loop will be implemented next.");

        var checkpointService = new CheckpointService(_serverId);
        Console.WriteLine($"[GameServer:{_serverId}] {checkpointService.GetType().Name} initialized.");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
        }
    }
}
