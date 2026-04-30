using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Gateway.Managers;
using Shared.Enums;
using Shared.Models;
using Shared.Protocol;

namespace Gateway.Handlers;

/// <summary>
/// Handles one internal plaintext TCP connection from a GameServer node.
/// Stage 1 responsibility:
/// - Receive NODE_REGISTER.
/// - Receive NODE_HEARTBEAT.
/// - Update NodeRegistry so later LoadBalancer/RecoveryCoordinator can use it.
/// </summary>
public sealed class GameServerHandler
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly NodeRegistry _nodeRegistry;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");
    public string? ServerId { get; private set; }

    public GameServerHandler(TcpClient tcpClient, NetworkStream stream, NodeRegistry nodeRegistry)
    {
        _tcpClient = tcpClient;
        _stream = stream;
        _nodeRegistry = nodeRegistry;
    }

    public async Task SendAsync(GameMessage message, CancellationToken cancellationToken = default)
    {
        var line = message.ToJsonLine();
        var bytes = Encoding.UTF8.GetBytes(line);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _stream.WriteAsync(bytes, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        var readBuffer = new byte[4096];
        var textBuffer = new StringBuilder();

        Console.WriteLine($"[Gateway][GameServer:{ShortId}] Handler started.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await _stream.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    Console.WriteLine($"[Gateway][GameServer:{ShortServerName}] Remote closed connection.");
                    break;
                }

                textBuffer.Append(Encoding.UTF8.GetString(readBuffer, 0, bytesRead));
                await ProcessBufferedLinesAsync(textBuffer, cancellationToken);
            }
        }
        finally
        {
            MarkNodeOfflineIfKnown();
        }
    }

    private async Task ProcessBufferedLinesAsync(StringBuilder textBuffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            var current = textBuffer.ToString();
            var newlineIndex = current.IndexOf('\n');
            if (newlineIndex < 0)
            {
                return;
            }

            var line = current[..newlineIndex].TrimEnd('\r').TrimStart('\uFEFF');
            textBuffer.Remove(0, newlineIndex + 1);

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await HandleLineAsync(line, cancellationToken);
        }
    }

    private Task HandleLineAsync(string line, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        try
        {
            var message = ParseInternalMessage(line);
            switch (message.Type)
            {
                case InternalMessageType.NodeRegister:
                    HandleNodeRegister(message);
                    break;

                case InternalMessageType.NodeHeartbeat:
                    HandleNodeHeartbeat(message);
                    break;

                default:
                    Console.WriteLine($"[Gateway][GameServer:{ShortServerName}] Internal message = {message.Type} (not handled in Stage 1)");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway][GameServer:{ShortServerName}] Invalid internal message. Error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private void HandleNodeRegister(InternalMessageEnvelope message)
    {
        var payload = message.Payload;
        var serverId = ReadString(payload, "serverId")
            ?? message.SenderId
            ?? $"gs-{ConnectionId[..8]}";

        var host = ReadString(payload, "host") ?? GetRemoteHost();
        var port = ReadInt(payload, "port", 0);
        var capacity = ReadInt(payload, "capacity", 0);

        ServerId = serverId;

        var heartbeat = new HeartbeatInfo(
            ServerId: serverId,
            ActiveRooms: 0,
            ActivePlayers: 0,
            CpuLoad: 0,
            MemoryUsage: 0,
            CanAcceptRoom: true,
            Status: NodeStatus.Online,
            LastSeen: DateTimeOffset.UtcNow);

        _nodeRegistry.UpsertHeartbeat(heartbeat);

        Console.WriteLine($"[Gateway] GameServer registered: {serverId} ({host}:{port}, capacity={capacity})");
    }

    private void HandleNodeHeartbeat(InternalMessageEnvelope message)
    {
        var payload = message.Payload;
        var serverId = ReadString(payload, "serverId")
            ?? message.SenderId
            ?? ServerId;

        if (string.IsNullOrWhiteSpace(serverId))
        {
            throw new InvalidOperationException("NODE_HEARTBEAT missing serverId.");
        }

        ServerId = serverId;

        var heartbeat = new HeartbeatInfo(
            ServerId: serverId,
            ActiveRooms: ReadInt(payload, "activeRooms", 0),
            ActivePlayers: ReadInt(payload, "activePlayers", 0),
            CpuLoad: ReadDouble(payload, "cpuLoad", 0),
            MemoryUsage: ReadDouble(payload, "memoryUsage", 0),
            CanAcceptRoom: ReadBool(payload, "canAcceptRoom", true),
            Status: ReadNodeStatus(payload, "status", NodeStatus.Online),
            LastSeen: DateTimeOffset.UtcNow);

        _nodeRegistry.UpsertHeartbeat(heartbeat);

        Console.WriteLine($"[Gateway] Heartbeat from {serverId}: rooms={heartbeat.ActiveRooms}, players={heartbeat.ActivePlayers}, canAccept={heartbeat.CanAcceptRoom}");
    }

    private void MarkNodeOfflineIfKnown()
    {
        if (string.IsNullOrWhiteSpace(ServerId))
        {
            return;
        }

        if (_nodeRegistry.TryGetNode(ServerId, out var lastHeartbeat) && lastHeartbeat is not null)
        {
            _nodeRegistry.UpsertHeartbeat(lastHeartbeat with
            {
                Status = NodeStatus.Offline,
                CanAcceptRoom = false,
                LastSeen = DateTimeOffset.UtcNow
            });
        }

        Console.WriteLine($"[Gateway] GameServer offline: {ServerId}");
    }

    private string ShortId => ConnectionId.Length <= 8 ? ConnectionId : ConnectionId[..8];
    private string ShortServerName => ServerId ?? ShortId;

    private string GetRemoteHost()
    {
        return _tcpClient.Client.RemoteEndPoint is IPEndPoint ipEndPoint
            ? ipEndPoint.Address.ToString()
            : "unknown";
    }

    private static InternalMessageEnvelope ParseInternalMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!TryGetPropertyIgnoreCase(root, "type", out var typeElement))
        {
            throw new InvalidOperationException("Missing 'type' property.");
        }

        var type = ParseInternalType(typeElement);
        JsonElement payload = default;
        if (TryGetPropertyIgnoreCase(root, "payload", out var payloadElement))
        {
            payload = payloadElement.Clone();
        }

        string? senderId = null;
        if (TryGetPropertyIgnoreCase(root, "senderId", out var senderElement))
        {
            senderId = senderElement.ValueKind == JsonValueKind.String ? senderElement.GetString() : senderElement.ToString();
        }

        return new InternalMessageEnvelope(type, payload, senderId);
    }

    private static InternalMessageType ParseInternalType(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return Enum.IsDefined(typeof(InternalMessageType), number)
                ? (InternalMessageType)number
                : InternalMessageType.Unknown;
        }

        var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return InternalMessageType.Unknown;
        }

        if (Enum.TryParse<InternalMessageType>(raw, ignoreCase: true, out var direct))
        {
            return direct;
        }

        var normalized = NormalizeToken(raw);
        foreach (var value in Enum.GetValues<InternalMessageType>())
        {
            if (NormalizeToken(value.ToString()) == normalized)
            {
                return value;
            }
        }

        return InternalMessageType.Unknown;
    }

    private static string? ReadString(JsonElement payload, string name)
    {
        if (TryGetPropertyIgnoreCase(payload, name, out var value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        return null;
    }

    private static int ReadInt(JsonElement payload, string name, int defaultValue)
    {
        if (TryGetPropertyIgnoreCase(payload, name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static double ReadDouble(JsonElement payload, string name, double defaultValue)
    {
        if (TryGetPropertyIgnoreCase(payload, name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static bool ReadBool(JsonElement payload, string name, bool defaultValue)
    {
        if (TryGetPropertyIgnoreCase(payload, name, out var value))
        {
            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static NodeStatus ReadNodeStatus(JsonElement payload, string name, NodeStatus defaultValue)
    {
        if (TryGetPropertyIgnoreCase(payload, name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && Enum.IsDefined(typeof(NodeStatus), number))
            {
                return (NodeStatus)number;
            }

            if (value.ValueKind == JsonValueKind.String && Enum.TryParse<NodeStatus>(value.GetString(), ignoreCase: true, out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
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

    private static string NormalizeToken(string value)
    {
        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }

    private sealed record InternalMessageEnvelope(InternalMessageType Type, JsonElement Payload, string? SenderId);
}