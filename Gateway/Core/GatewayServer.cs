using Gateway.Data;
using Gateway.Managers;
using Gateway.Services;

namespace Gateway.Core;

public sealed class GatewayServer
{
    private readonly int _port;
    private readonly NodeRegistry _nodeRegistry = new();
    private readonly RoomDirectory _roomDirectory = new();
    private readonly SessionDirectory _sessionDirectory = new();
    private readonly CheckpointStore _checkpointStore = new();

    public GatewayServer(int port)
    {
        _port = port;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Gateway] Starting on port {_port}");
        Console.WriteLine("[Gateway] Baseline mode: skeleton is ready. Socket accept loop will be implemented next.");

        var userRepository = new UserRepository("Data Source=pictionary.db");
        await userRepository.InitializeAsync(cancellationToken);

        var loadBalancer = new LoadBalancer(_nodeRegistry);
        var proxyRouter = new ProxyRouter(_roomDirectory, _sessionDirectory);
        var recoveryCoordinator = new RecoveryCoordinator(_nodeRegistry, _roomDirectory, _checkpointStore);

        Console.WriteLine("[Gateway] Services initialized:");
        Console.WriteLine($"  - {loadBalancer.GetType().Name}");
        Console.WriteLine($"  - {proxyRouter.GetType().Name}");
        Console.WriteLine($"  - {recoveryCoordinator.GetType().Name}");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
        }
    }
}
