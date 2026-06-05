using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gateway.Data;
using Gateway.Handlers;
using Gateway.Managers;
using Gateway.Security;
using Gateway.Services;
using Shared.Enums;
using Shared.Models;

namespace Gateway.Core;

/// <summary>
/// Điểm vào chính của hệ thống Gateway.
/// Trách nhiệm:
/// - Lắng nghe kết nối từ Client qua cổng TLS (Bảo mật).
/// - Lắng nghe kết nối từ GameServer qua cổng TCP nội bộ.
/// - Khởi tạo và quản lý vòng đời của các Handler.
/// </summary>
public sealed class GatewayServer
{
    private static readonly TimeSpan NodeHeartbeatTimeout = TimeSpan.FromSeconds(9);
    private static readonly TimeSpan NodeMonitorInterval = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions MessageJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly int _clientPort;
    private readonly int _gameServerPort;

    // Quản lý trạng thái trong bộ nhớ RAM
    private readonly NodeRegistry _nodeRegistry = new();
    private readonly RoomDirectory _roomDirectory = new();
    private readonly SessionDirectory _sessionDirectory = new();
    private readonly GameServerConnectionDirectory _gameServerConnections = new();
    private readonly ClientConnectionDirectory _clientConnections = new();
    private readonly CheckpointStore _checkpointStore = new();

    // Các công cụ hỗ trợ kết nối và bảo mật
    private readonly TlsServerFactory _tlsServerFactory = new();
    private AuthService? _authService;
    private PersistenceService? _persistenceService;
    private RecoveryCoordinator? _recoveryCoordinator;

    public GatewayServer(int clientPort = 5000, int gameServerPort = 6000)
    {
        _clientPort = clientPort;
        _gameServerPort = gameServerPort;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[Gateway] Đang khởi động hệ thống...");
        Console.WriteLine($"[Gateway] Cổng TLS cho Client     : {_clientPort}");
        Console.WriteLine($"[Gateway] Cổng TCP cho GameServer : {_gameServerPort}");

        // 1. Khởi tạo Database (Lớp Dữ liệu)
        const string connectionString = "Data Source=pictionary.db";
        var userRepository = new UserRepository(connectionString);
        await userRepository.InitializeAsync(cancellationToken);
        Console.WriteLine("[Gateway] Database SQLite đã sẵn sàng.");

        // 2. Khởi tạo các Dịch vụ Nghiệp vụ (Lớp Service)
        var sessionService = new SessionService();
        _authService = new AuthService(userRepository, sessionService);
        _persistenceService = new PersistenceService(connectionString);

        var loadBalancer = new LoadBalancer(_nodeRegistry);
        var proxyRouter = new ProxyRouter(_roomDirectory, _sessionDirectory);
        _recoveryCoordinator = new RecoveryCoordinator(
            _nodeRegistry,
            _roomDirectory,
            _checkpointStore,
            _gameServerConnections,
            _clientConnections);

        Console.WriteLine("[Gateway] Các dịch vụ nội bộ đã khởi tạo:");
        Console.WriteLine($"  - Xác thực người dùng (AuthService)");
        Console.WriteLine($"  - Cân bằng tải ({loadBalancer.GetType().Name})");
        Console.WriteLine($"  - Định tuyến tin nhắn ({proxyRouter.GetType().Name})");

        // 3. Khởi tạo bộ lắng nghe (Listeners)
        var clientListener = _tlsServerFactory.CreateListener(_clientPort);
        var gameServerListener = new TcpListener(IPAddress.Any, _gameServerPort);

        try
        {
            clientListener.Start();
            gameServerListener.Start();

            Console.WriteLine("[Gateway] Đang chờ kết nối từ Clients và GameServers...");

            // Chạy song song hai luồng chấp nhận kết nối
            var clientAcceptLoop = AcceptClientsAsync(clientListener, cancellationToken);
            var gameServerAcceptLoop = AcceptGameServersAsync(gameServerListener, cancellationToken);
            var nodeMonitorLoop = MonitorGameServerHeartbeatsAsync(cancellationToken);

            await Task.WhenAll(clientAcceptLoop, gameServerAcceptLoop, nodeMonitorLoop);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway] Lỗi nghiêm trọng khi chạy Server: {ex.Message}");
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
                tcpClient.NoDelay = true; // <--- THÊM DÒNG
                // Tạo một Task riêng để xử lý mỗi Client, tránh làm nghẽn luồng chấp nhận
                _ = Task.Run(() => HandleClientConnectionAsync(tcpClient, cancellationToken), CancellationToken.None);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Console.WriteLine($"[Gateway][ClientAccept] Lỗi chấp nhận kết nối: {ex.Message}");
            }
        }
    }

    private async Task HandleClientConnectionAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        var remote = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            using (tcpClient)
            // Thiết lập bắt tay TLS (Handshake) trước khi truyền nhận dữ liệu
            using (var tlsStream = await _tlsServerFactory.AuthenticateAsServerAsync(tcpClient, cancellationToken))
            {
                Console.WriteLine($"[Gateway] Client kết nối an toàn (TLS): {remote}");

                if (_authService is null)
                {
                    throw new InvalidOperationException("AuthService chưa được khởi tạo.");
                }

                // Truyền đầy đủ dịch vụ vào Handler để xử lý Login/Register
                var handler = new ClientHandler(
                    tlsStream,
                    _authService,
                    _sessionDirectory,
                    _roomDirectory,
                    _nodeRegistry,
                    new LoadBalancer(_nodeRegistry),
                    _gameServerConnections,
                    _clientConnections,
                    _persistenceService);
                await handler.HandleAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            Console.WriteLine($"[Gateway][Client:{remote}] Ngắt kết nối/Lỗi: {ex.Message}");
        }
    }

    private async Task MonitorGameServerHeartbeatsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(NodeMonitorInterval, cancellationToken);
            var timedOutServers = _nodeRegistry.MarkTimedOutNodesOffline(NodeHeartbeatTimeout);

            foreach (var serverId in timedOutServers)
            {
                _gameServerConnections.Remove(serverId);
                Console.WriteLine($"[Gateway] GameServer heartbeat timeout: {serverId}");
                _ = Task.Run(() => RecoverRoomsOwnedByAsync(serverId, CancellationToken.None), CancellationToken.None);
            }
        }
    }

    private async Task AcceptGameServersAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
                tcpClient.NoDelay = true; // <--- THÊM DÒNG NÀY
                _ = Task.Run(() => HandleGameServerConnectionAsync(tcpClient, cancellationToken), CancellationToken.None);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Console.WriteLine($"[Gateway][GameServerAccept] Lỗi chấp nhận kết nối: {ex.Message}");
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
                Console.WriteLine($"[Gateway] GameServer kết nối (TCP): {remote}");

                // GameServer kết nối nội bộ nên dùng stream trực tiếp (plaintext)
                var handler = new GameServerHandler(
                    tcpClient,
                    stream,
                    _nodeRegistry,
                    _gameServerConnections,
                    HandleServerEventAsync,
                    HandleMatchResultAsync,
                    HandleRoomCheckpointAsync,
                    RecoverRoomsOwnedByAsync);
                await handler.HandleAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException))
        {
            Console.WriteLine($"[Gateway][GameServer:{remote}] Ngắt kết nối/Lỗi: {ex.Message}");
        }
    }

    private async Task HandleServerEventAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (!TryReadString(payload, "roomCode", out var roomCode) || string.IsNullOrWhiteSpace(roomCode))
        {
            return;
        }

        if (!TryGetPropertyIgnoreCase(payload, "innerMessage", out var innerMessageElement))
        {
            return;
        }

        var message = innerMessageElement.Deserialize<GameMessage>(MessageJsonOptions);
        if (message is null)
        {
            return;
        }

        IReadOnlyList<Handlers.ClientHandler> targets;
        if (TryGetPropertyIgnoreCase(payload, "targetSessionIds", out var targetSessionIdsElement) &&
            targetSessionIdsElement.ValueKind == JsonValueKind.Array)
        {
            var sessionIds = targetSessionIdsElement
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(sessionId => !string.IsNullOrWhiteSpace(sessionId))
                .Select(sessionId => sessionId!)
                .ToList();

            targets = _clientConnections.GetClients(sessionIds);
        }
        else
        {
            targets = _clientConnections.GetRoomClients(roomCode);
        }

        foreach (var client in targets)
        {
            await client.SendAsync(message, cancellationToken);
        }

        if (message.Type == MessageType.GameEnd)
        {
            _sessionDirectory.ClearRoom(roomCode);
            _clientConnections.RemoveRoom(roomCode);
            _roomDirectory.Remove(roomCode);
        }
    }

    private async Task HandleMatchResultAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (_persistenceService is null)
        {
            return;
        }

        var result = payload.Deserialize<MatchResult>(MessageJsonOptions);
        if (result is null)
        {
            return;
        }

        // Member D: persist completed matches after gameplay leaves the realtime hot path.
        await _persistenceService.SaveMatchResultAsync(result, cancellationToken);
        Console.WriteLine($"[Gateway] Match persisted for room {result.RoomCode}.");
    }

    private Task HandleRoomCheckpointAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var snapshot = payload.Deserialize<RoomSnapshot>(MessageJsonOptions);
        if (snapshot is null)
        {
            return Task.CompletedTask;
        }

        // Stage 6 - A: keep the latest room checkpoint in memory for fast failover.
        _checkpointStore.Save(snapshot);
        return Task.CompletedTask;
    }

    private Task RecoverRoomsOwnedByAsync(string failedServerId, CancellationToken cancellationToken)
    {
        return _recoveryCoordinator is null
            ? Task.CompletedTask
            : _recoveryCoordinator.RecoverRoomsOwnedByAsync(failedServerId, cancellationToken);
    }

    private static bool TryReadString(JsonElement element, string name, out string? value)
    {
        if (TryGetPropertyIgnoreCase(element, name, out var property))
        {
            value = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
