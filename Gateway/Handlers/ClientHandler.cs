using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gateway.Managers;
using Gateway.Services;
using Shared.Enums;
using Shared.Models;
using Shared.Protocol;

namespace Gateway.Handlers;

public sealed class ClientHandler
{
    private readonly Stream _stream;
    private readonly AuthService _authService;
    private readonly SessionDirectory _sessionDirectory;
    private readonly RoomDirectory? _roomDirectory;
    private readonly NodeRegistry? _nodeRegistry;
    private readonly LoadBalancer? _loadBalancer;
    private readonly ProxyRouter? _proxyRouter;
    private readonly GameServerConnectionDirectory? _gameServerConnections;
    private readonly ClientConnectionDirectory? _clientConnections;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private static readonly JsonSerializerOptions RoomJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private string? _currentSessionId;

    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

    public ClientHandler(Stream stream, AuthService authService, SessionDirectory sessionDirectory)
    {
        _stream = stream;
        _authService = authService;
        _sessionDirectory = sessionDirectory;
    }

    public ClientHandler(
        Stream stream,
        AuthService authService,
        SessionDirectory sessionDirectory,
        RoomDirectory roomDirectory,
        NodeRegistry nodeRegistry,
        LoadBalancer loadBalancer,
        GameServerConnectionDirectory gameServerConnections,
        ClientConnectionDirectory clientConnections)
        : this(stream, authService, sessionDirectory)
    {
        _roomDirectory = roomDirectory;
        _nodeRegistry = nodeRegistry;
        _loadBalancer = loadBalancer;
        _gameServerConnections = gameServerConnections;
        _clientConnections = clientConnections;
        _proxyRouter = new ProxyRouter(roomDirectory, sessionDirectory, gameServerConnections);
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

        Console.WriteLine($"[Gateway][Client:{ShortId}] Handler started.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await _stream.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    Console.WriteLine($"[Gateway][Client:{ShortId}] Remote closed connection.");
                    break;
                }

                textBuffer.Append(Encoding.UTF8.GetString(readBuffer, 0, bytesRead));
                await ProcessBufferedLinesAsync(textBuffer, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway][Client:{ShortId}] Stream error: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(_currentSessionId))
            {
                _clientConnections?.RemoveSession(_currentSessionId);
            }
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

            if (!string.IsNullOrWhiteSpace(line))
            {
                await HandleLineAsync(line, cancellationToken);
            }
        }
    }

    private async Task HandleLineAsync(string line, CancellationToken cancellationToken)
    {
        try
        {
            var message = ParseGameMessage(line);
            Console.WriteLine($"[Gateway][Client:{ShortId}] Message type = {message.Type}");

            switch (message.Type)
            {
                case MessageType.Register:
                    await HandleRegisterAsync(message, cancellationToken);
                    break;
                case MessageType.Login:
                    await HandleLoginAsync(message, cancellationToken);
                    break;
                case MessageType.CreateRoom:
                    await HandleCreateRoomAsync(message, cancellationToken);
                    break;
                case MessageType.Join:
                    await HandleJoinRoomAsync(message, cancellationToken);
                    break;
                case MessageType.GetRoomList:
                    await HandleGetRoomListAsync(cancellationToken);
                    break;
                case MessageType.Ready:
                case MessageType.SelectWord:
                case MessageType.Draw:
                case MessageType.Guess:
                case MessageType.Chat:
                    await HandleGameplayMessageAsync(message, cancellationToken);
                    break;
                default:
                    await SendAsync(CreateError(MessageType.Error, $"{message.Type} is not supported yet."), cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway][Client:{ShortId}] Message handling error: {ex.Message}");
            await SendAsync(CreateError(MessageType.Error, ex.Message), cancellationToken);
        }
    }

    private async Task HandleRegisterAsync(GameMessage message, CancellationToken cancellationToken)
    {
        var (username, password) = ReadCredentials(message.Payload);
        var validationError = ValidateCredentials(username, password);
        if (validationError is not null)
        {
            await SendAsync(CreateError(MessageType.RegisterFailed, validationError), cancellationToken);
            return;
        }

        try
        {
            var userId = await _authService.RegisterAsync(username, password, cancellationToken);
            Console.WriteLine($"[Gateway][Client:{ShortId}] Register success: {username}");

            await SendAsync(new GameMessage
            {
                Type = MessageType.RegisterSuccess,
                Payload = new { userId, playerId = userId, username }
            }, cancellationToken);
        }
        catch
        {
            await SendAsync(CreateError(MessageType.RegisterFailed, "Username already exists or registration data is invalid."), cancellationToken);
        }
    }

    private async Task HandleLoginAsync(GameMessage message, CancellationToken cancellationToken)
    {
        var (username, password) = ReadCredentials(message.Payload);
        var sessionId = await _authService.LoginAsync(username, password, cancellationToken);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Console.WriteLine($"[Gateway][Client:{ShortId}] Login failed: {username}");
            await SendAsync(CreateError(MessageType.LoginFailed, "Invalid username or password."), cancellationToken);
            return;
        }

        var userId = ExtractUserIdFromSessionId(sessionId);
        var session = new SessionInfo(
            SessionId: sessionId,
            UserId: userId,
            PlayerId: userId,
            RoomCode: null,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(4));

        _sessionDirectory.Set(session);
        _currentSessionId = sessionId;
        _clientConnections?.RegisterSession(sessionId, this);

        Console.WriteLine($"[Gateway][Client:{ShortId}] Login success: {username}");

        await SendAsync(new GameMessage
        {
            Type = MessageType.LoginSuccess,
            Payload = new { sessionId, userId, playerId = userId, username }
        }, cancellationToken);
    }

    private async Task HandleCreateRoomAsync(GameMessage message, CancellationToken cancellationToken)
    {
        if (!TryResolveSession(message.Payload, out var session, out var sessionId, out var error))
        {
            await SendAsync(CreateError(MessageType.Error, error), cancellationToken);
            return;
        }
        var resolvedSession = session!;

        if (_loadBalancer is null || _gameServerConnections is null || _roomDirectory is null || _nodeRegistry is null)
        {
            await SendAsync(CreateError(MessageType.Error, "Gateway room routing is not configured."), cancellationToken);
            return;
        }

        var selectedServer = _loadBalancer.SelectBestServer();
        if (selectedServer is null ||
            !_gameServerConnections.TryGet(selectedServer.ServerId, out var gameServer) ||
            gameServer is null)
        {
            await SendAsync(CreateError(MessageType.Error, "No GameServer is available for new rooms."), cancellationToken);
            return;
        }

        var playerName = ReadStringFromPayload(message.Payload, "playerName")
            ?? ReadStringFromPayload(message.Payload, "username")
            ?? resolvedSession.UserId;

        var response = await gameServer.SendInternalRequestAsync(
            InternalMessageType.CreateRoomOnNode,
            new { sessionId, playerId = resolvedSession.PlayerId, playerName },
            cancellationToken);

        await ApplyRoomResponseAsync(response, sessionId, selectedServer.ServerId, isCreateRoom: true, cancellationToken);
    }

    private async Task HandleJoinRoomAsync(GameMessage message, CancellationToken cancellationToken)
    {
        if (!TryResolveSession(message.Payload, out var session, out var sessionId, out var error))
        {
            await SendAsync(CreateError(MessageType.Error, error), cancellationToken);
            return;
        }
        var resolvedSession = session!;

        if (_gameServerConnections is null || _roomDirectory is null)
        {
            await SendAsync(CreateError(MessageType.Error, "Gateway room routing is not configured."), cancellationToken);
            return;
        }

        var roomCode = ReadStringFromPayload(message.Payload, "roomCode")?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            await SendAsync(CreateError(MessageType.Error, "Room code is required."), cancellationToken);
            return;
        }

        if (!_roomDirectory.TryGetOwner(roomCode, out var ownerServerId) ||
            string.IsNullOrWhiteSpace(ownerServerId) ||
            !_gameServerConnections.TryGet(ownerServerId, out var gameServer) ||
            gameServer is null)
        {
            await SendAsync(CreateError(MessageType.Error, "Room not found."), cancellationToken);
            return;
        }

        var playerName = ReadStringFromPayload(message.Payload, "playerName")
            ?? ReadStringFromPayload(message.Payload, "username")
            ?? resolvedSession.UserId;

        var response = await gameServer.SendInternalRequestAsync(
            InternalMessageType.ForwardClientMessage,
            new
            {
                roomCode,
                sessionId,
                playerId = resolvedSession.PlayerId,
                playerName,
                innerMessage = message
            },
            cancellationToken);

        await ApplyRoomResponseAsync(response, sessionId, ownerServerId, isCreateRoom: false, cancellationToken);
    }

    private async Task HandleGetRoomListAsync(CancellationToken cancellationToken)
    {
        if (_roomDirectory is null)
        {
            await SendAsync(CreateError(MessageType.Error, "Room directory is not configured."), cancellationToken);
            return;
        }

        await SendAsync(new GameMessage
        {
            Type = MessageType.RoomList,
            Payload = new { rooms = _roomDirectory.GetWaitingRooms() }
        }, cancellationToken);
    }

    private async Task HandleGameplayMessageAsync(GameMessage message, CancellationToken cancellationToken)
    {
        if (_proxyRouter is null)
        {
            await SendAsync(CreateError(MessageType.Error, "Gateway proxy router is not configured."), cancellationToken);
            return;
        }

        var roomCode = ReadStringFromPayload(message.Payload, "roomCode")?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            await SendAsync(CreateError(MessageType.Error, "Room code is required."), cancellationToken);
            return;
        }

        if (!TryResolveSession(message.Payload, out _, out var sessionId, out var error))
        {
            await SendAsync(CreateError(MessageType.Error, error), cancellationToken);
            return;
        }

        try
        {
            await _proxyRouter.RouteClientMessageAsync(roomCode, sessionId, message, cancellationToken);
        }
        catch (Exception ex)
        {
            await SendAsync(CreateError(MessageType.Error, ex.Message), cancellationToken);
        }
    }

    private async Task ApplyRoomResponseAsync(
        JsonElement response,
        string sessionId,
        string ownerServerId,
        bool isCreateRoom,
        CancellationToken cancellationToken)
    {
        var success = ReadBoolProperty(response, "success", false);
        if (!success)
        {
            await SendAsync(CreateError(MessageType.Error, ReadStringProperty(response, "error") ?? "Room operation failed."), cancellationToken);
            return;
        }

        var roomCode = ReadStringProperty(response, "roomCode");
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            await SendAsync(CreateError(MessageType.Error, "GameServer did not return a room code."), cancellationToken);
            return;
        }

        RoomInfo? roomInfo = null;
        if (TryGetPropertyIgnoreCase(response, "roomInfo", out var roomInfoElement))
        {
            roomInfo = roomInfoElement.Deserialize<RoomInfo>(RoomJsonOptions);
        }

        if (roomInfo is not null)
        {
            _roomDirectory?.UpsertRoom(roomInfo);
        }
        else
        {
            _roomDirectory?.SetOwner(roomCode, ownerServerId);
        }

        _clientConnections?.JoinRoom(roomCode, sessionId);

        if (_sessionDirectory.TryGet(sessionId, out var session) && session is not null)
        {
            _sessionDirectory.Set(session with { RoomCode = roomCode });
        }

        _nodeRegistry?.AdjustLoad(ownerServerId, isCreateRoom ? 1 : 0, 1);

        await SendAsync(new GameMessage
        {
            Type = MessageType.RoomJoined,
            Payload = new { roomCode, roomInfo }
        }, cancellationToken);

        if (TryGetPropertyIgnoreCase(response, "players", out var playersElement))
        {
            var playerListMessage = new GameMessage
            {
                Type = MessageType.PlayerList,
                Payload = new { roomCode, players = playersElement.Clone() }
            };

            if (_clientConnections is null)
            {
                await SendAsync(playerListMessage, cancellationToken);
                return;
            }

            foreach (var client in _clientConnections.GetRoomClients(roomCode))
            {
                await client.SendAsync(playerListMessage, cancellationToken);
            }
        }
    }

    private string ShortId => ConnectionId.Length <= 8 ? ConnectionId : ConnectionId[..8];

    private bool TryResolveSession(object? payload, out SessionInfo? session, out string sessionId, out string error)
    {
        sessionId = ReadStringFromPayload(payload, "sessionId") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            session = null;
            error = "SessionId is required.";
            return false;
        }

        if (!_sessionDirectory.TryGet(sessionId, out session) || session is null)
        {
            error = "Invalid session.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static (string Username, string Password) ReadCredentials(object? payload)
    {
        return (
            ReadStringFromPayload(payload, "username") ?? string.Empty,
            ReadStringFromPayload(payload, "password") ?? string.Empty);
    }

    private static string? ReadStringFromPayload(object? payload, string name)
    {
        if (payload is JsonElement element)
        {
            return ReadStringProperty(element, name);
        }

        if (payload is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(payload, GameMessage.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return ReadStringProperty(doc.RootElement, name);
    }

    private static string? ReadStringProperty(JsonElement element, string name)
    {
        if (TryGetPropertyIgnoreCase(element, name, out var property))
        {
            return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        }

        return null;
    }

    private static bool ReadBoolProperty(JsonElement element, string name, bool defaultValue)
    {
        if (!TryGetPropertyIgnoreCase(element, name, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static string? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            return "Username must be at least 3 characters.";
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            return "Password must be at least 6 characters.";
        }

        return null;
    }

    private static string ExtractUserIdFromSessionId(string sessionId)
    {
        var separatorIndex = sessionId.IndexOf(':');
        return separatorIndex > 0 ? sessionId[..separatorIndex] : sessionId;
    }

    private static GameMessage CreateError(MessageType type, string message)
    {
        return new GameMessage { Type = type, Payload = new { message } };
    }

    private static GameMessage ParseGameMessage(string json)
    {
        try
        {
            var baselineMessage = GameMessage.FromJson(json);
            if (baselineMessage is not null && baselineMessage.Type != MessageType.Unknown)
            {
                return baselineMessage;
            }
        }
        catch
        {
            // Fall back to document-friendly string message types such as "Register" or "LOGIN".
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!TryGetPropertyIgnoreCase(root, "type", out var typeElement))
        {
            throw new InvalidOperationException("Missing 'type' property.");
        }

        object? payload = null;
        if (TryGetPropertyIgnoreCase(root, "payload", out var payloadElement))
        {
            payload = payloadElement.Clone();
        }

        string? senderId = null;
        if (TryGetPropertyIgnoreCase(root, "senderId", out var senderElement))
        {
            senderId = senderElement.ValueKind == JsonValueKind.String
                ? senderElement.GetString()
                : senderElement.ToString();
        }

        return new GameMessage
        {
            Type = ParseMessageType(typeElement),
            Payload = payload,
            SenderId = senderId,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    private static MessageType ParseMessageType(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
        {
            return Enum.IsDefined(typeof(MessageType), number) ? (MessageType)number : MessageType.Unknown;
        }

        var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return MessageType.Unknown;
        }

        if (Enum.TryParse<MessageType>(raw, ignoreCase: true, out var direct))
        {
            return direct;
        }

        var normalized = NormalizeToken(raw);
        foreach (var value in Enum.GetValues<MessageType>())
        {
            if (NormalizeToken(value.ToString()) == normalized)
            {
                return value;
            }
        }

        return MessageType.Unknown;
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
}
