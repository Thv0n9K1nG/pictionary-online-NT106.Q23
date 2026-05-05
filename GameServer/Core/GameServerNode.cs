using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameServer.Managers;
using GameServer.Services;
using Shared.Enums;
using Shared.Protocol;

namespace GameServer.Core;

public sealed class GameServerNode
{
    private const int HeartbeatIntervalSeconds = 3;
    private const int ReconnectDelaySeconds = 3;

    private readonly string _serverId;
    private readonly string _gatewayHost;
    private readonly int _gatewayPort;
    private readonly RoomManager _roomManager = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

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

        var checkpointService = new CheckpointService(_serverId);
        Console.WriteLine($"[GameServer:{_serverId}] {checkpointService.GetType().Name} initialized.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunGatewayConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServer:{_serverId}] Gateway connection error: {ex.Message}");
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine($"[GameServer:{_serverId}] Reconnecting in {ReconnectDelaySeconds}s...");
                await Task.Delay(TimeSpan.FromSeconds(ReconnectDelaySeconds), cancellationToken);
            }
        }
    }

    private async Task RunGatewayConnectionAsync(CancellationToken cancellationToken)
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(_gatewayHost, _gatewayPort, cancellationToken);

        Console.WriteLine($"[GameServer:{_serverId}] Connected to Gateway internal TCP.");

        await using var stream = tcpClient.GetStream();
        await SendNodeRegisterAsync(stream, cancellationToken);

        while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
        {
            await SendNodeHeartbeatAsync(stream, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), cancellationToken);
        }
    }

    private Task SendNodeRegisterAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var payload = new
        {
            serverId = _serverId,
            host = Environment.MachineName,
            port = 0,
            capacity = 100
        };

        Console.WriteLine($"[GameServer:{_serverId}] Sending NODE_REGISTER.");
        return SendInternalMessageAsync(stream, InternalMessageType.NodeRegister, payload, cancellationToken);
    }

    private Task SendNodeHeartbeatAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var payload = new
        {
            serverId = _serverId,
            activeRooms = _roomManager.ActiveRoomCount,
            activePlayers = _roomManager.ActivePlayerCount,
            cpuLoad = 0,
            memoryUsage = Math.Round(Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d, 2),
            canAcceptRoom = true,
            status = NodeStatus.Online
        };

        Console.WriteLine($"[GameServer:{_serverId}] Sending NODE_HEARTBEAT.");
        return SendInternalMessageAsync(stream, InternalMessageType.NodeHeartbeat, payload, cancellationToken);
    }

    private async Task SendInternalMessageAsync(
        NetworkStream stream,
        InternalMessageType type,
        object payload,
        CancellationToken cancellationToken)
    {
        var envelope = new
        {
            type,
            payload,
            senderId = _serverId,
            timestamp = DateTimeOffset.UtcNow
        };

        var line = JsonSerializer.Serialize(envelope, _jsonOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
