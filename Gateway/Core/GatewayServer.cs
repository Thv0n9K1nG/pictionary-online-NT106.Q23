using System.Net;
using System.Net.Sockets;
using Gateway.Data;
using Gateway.Handlers;
using Gateway.Managers;
using Gateway.Security;
using Gateway.Services;

namespace Gateway.Core;

/// <summary>
/// Gateway entry point for the system.
/// Stage 1 responsibility:
/// - Listen for Client connections on a TLS port.
/// - Listen for GameServer node connections on an internal plaintext TCP port.
/// - Spawn one handler per accepted connection.
/// </summary>
public sealed class GatewayServer
{
    private readonly int _clientPort;
    private readonly int _gameServerPort;

    private readonly NodeRegistry _nodeRegistry = new();
    private readonly RoomDirectory _roomDirectory = new();
    private readonly SessionDirectory _sessionDirectory = new();
    private readonly CheckpointStore _checkpointStore = new();
    private readonly TlsServerFactory _tlsServerFactory = new();

    public GatewayServer(int clientPort = 5000, int gameServerPort = 6000)
    {
        _clientPort = clientPort;
        _gameServerPort = gameServerPort;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Gateway] Starting");
        Console.WriteLine($"[Gateway] Client TLS port     : {_clientPort}");
        Console.WriteLine($"[Gateway] GameServer TCP port: {_gameServerPort}");

        // Keep baseline services initialized so Stage 2/3 can continue from here.
        var userRepository = new UserRepository("Data Source=pictionary.db");
        await userRepository.InitializeAsync(cancellationToken);

        var loadBalancer = new LoadBalancer(_nodeRegistry);
        var proxyRouter = new ProxyRouter(_roomDirectory, _sessionDirectory);
        var recoveryCoordinator = new RecoveryCoordinator(_nodeRegistry, _roomDirectory, _checkpointStore);

        Console.WriteLine("[Gateway] Services initialized:");
        Console.WriteLine($"  - {loadBalancer.GetType().Name}");
        Console.WriteLine($"  - {proxyRouter.GetType().Name}");
        Console.WriteLine($"  - {recoveryCoordinator.GetType().Name}");

        var clientListener = _tlsServerFactory.CreateListener(_clientPort);
        var gameServerListener = new TcpListener(IPAddress.Any, _gameServerPort);

        try
        {
            clientListener.Start();
            gameServerListener.Start();

            Console.WriteLine("[Gateway] Listening for clients and GameServer nodes...");

            var clientAcceptLoop = AcceptClientsAsync(clientListener, cancellationToken);
            var gameServerAcceptLoop = AcceptGameServersAsync(gameServerListener, cancellationToken);

            await Task.WhenAll(clientAcceptLoop, gameServerAcceptLoop);
        }
        finally
        {
            clientListener.Stop();
            gameServerListener.Stop();
        }
    }

    private async Task AcceptClientsAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleClientConnectionAsync(tcpClient, cancellationToken), CancellationToken.None);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gateway][ClientAccept] Error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientConnectionAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var remote = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            using (tcpClient)
            using (var tlsStream = await _tlsServerFactory.AuthenticateAsServerAsync(tcpClient, cancellationToken))
            {
                Console.WriteLine($"[Gateway] Client connected over TLS: {remote}");
                var handler = new ClientHandler(tcpClient, tlsStream);
                await handler.HandleAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway][Client:{remote}] Disconnected/Error: {ex.Message}");
        }
    }

    private async Task AcceptGameServersAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleGameServerConnectionAsync(tcpClient, cancellationToken), CancellationToken.None);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gateway][GameServerAccept] Error: {ex.Message}");
            }
        }
    }

    private async Task HandleGameServerConnectionAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var remote = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            using (tcpClient)
            using (var stream = tcpClient.GetStream())
            {
                Console.WriteLine($"[Gateway] GameServer TCP connection accepted: {remote}");
                var handler = new GameServerHandler(tcpClient, stream, _nodeRegistry);
                await handler.HandleAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway][GameServer:{remote}] Disconnected/Error: {ex.Message}");
        }
    }
}