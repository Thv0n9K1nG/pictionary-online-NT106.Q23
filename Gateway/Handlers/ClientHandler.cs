using System.Text;
using System.Text.Json;
using Gateway.Managers;
using Gateway.Services;
using Shared.Enums;
using Shared.Models;

namespace Gateway.Handlers;

/// <summary>
/// Handles one Client connection after TLS has been established by GatewayServer.
/// Stage 1 only logs incoming GameMessage type. Stage 2 will route REGISTER/LOGIN
/// to AuthService and later relay room/gameplay messages to ProxyRouter.
/// </summary>
public sealed class ClientHandler
{
    private readonly Stream _stream;
    private readonly AuthService? _authService;
    private readonly SessionDirectory? _sessionDirectory;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

    public ClientHandler(System.Net.Sockets.TcpClient tcpClient, Stream stream)
        : this(stream)
    {
        _ = tcpClient;
    }

    public ClientHandler(Stream stream)
    {
        _stream = stream;
    }

    public ClientHandler(
        System.Net.Sockets.TcpClient tcpClient,
        Stream stream,
        AuthService authService,
        SessionDirectory sessionDirectory)
        : this(stream, authService, sessionDirectory)
    {
        _ = tcpClient;
    }

    public ClientHandler(Stream stream, AuthService authService, SessionDirectory sessionDirectory)
    {
        _stream = stream;
        _authService = authService;
        _sessionDirectory = sessionDirectory;
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

                default:
                    Console.WriteLine($"[Gateway][Client:{ShortId}] Message type = {message.Type} (not handled in Stage 2)");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway][Client:{ShortId}] Invalid message. Error: {ex.Message}");
        }
    }

    private string ShortId => ConnectionId.Length <= 8 ? ConnectionId : ConnectionId[..8];

    private async Task HandleRegisterAsync(GameMessage message, CancellationToken cancellationToken)
    {
        if (_authService is null)
        {
            await SendAsync(CreateError(MessageType.RegisterFailed, "AuthService is not configured."), cancellationToken);
            return;
        }

        var credentials = ReadCredentials(message.Payload);
        var validationError = ValidateCredentials(credentials.Username, credentials.Password);
        if (validationError is not null)
        {
            await SendAsync(CreateError(MessageType.RegisterFailed, validationError), cancellationToken);
            return;
        }

        try
        {
            var userId = await _authService.RegisterAsync(credentials.Username, credentials.Password, cancellationToken);
            Console.WriteLine($"[Gateway][Client:{ShortId}] Register success: username={credentials.Username}, userId={userId}");

            await SendAsync(new GameMessage
            {
                Type = MessageType.RegisterSuccess,
                Payload = new
                {
                    userId,
                    playerId = userId,
                    username = credentials.Username
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway][Client:{ShortId}] Register failed: {ex.Message}");
            await SendAsync(CreateError(MessageType.RegisterFailed, "Username already exists or registration data is invalid."), cancellationToken);
        }
    }

    private async Task HandleLoginAsync(GameMessage message, CancellationToken cancellationToken)
    {
        if (_authService is null || _sessionDirectory is null)
        {
            await SendAsync(CreateError(MessageType.LoginFailed, "AuthService is not configured."), cancellationToken);
            return;
        }

        var credentials = ReadCredentials(message.Payload);
        var validationError = ValidateCredentials(credentials.Username, credentials.Password);
        if (validationError is not null)
        {
            await SendAsync(CreateError(MessageType.LoginFailed, validationError), cancellationToken);
            return;
        }

        var sessionId = await _authService.LoginAsync(credentials.Username, credentials.Password, cancellationToken);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Console.WriteLine($"[Gateway][Client:{ShortId}] Login failed: username={credentials.Username}");
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
            ExpiresAt: null);

        _sessionDirectory.Set(session);
        Console.WriteLine($"[Gateway][Client:{ShortId}] Login success: username={credentials.Username}, userId={userId}");

        await SendAsync(new GameMessage
        {
            Type = MessageType.LoginSuccess,
            Payload = new
            {
                sessionId,
                userId,
                playerId = userId,
                username = credentials.Username
            }
        }, cancellationToken);
    }

    private static (string Username, string Password) ReadCredentials(object? payload)
    {
        if (payload is JsonElement element)
        {
            return (
                ReadStringProperty(element, "username") ?? string.Empty,
                ReadStringProperty(element, "password") ?? string.Empty);
        }

        if (payload is null)
        {
            return (string.Empty, string.Empty);
        }

        var json = JsonSerializer.Serialize(payload, GameMessage.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return (
            ReadStringProperty(doc.RootElement, "username") ?? string.Empty,
            ReadStringProperty(doc.RootElement, "password") ?? string.Empty);
    }

    private static string? ReadStringProperty(JsonElement element, string name)
    {
        return TryGetPropertyIgnoreCase(element, name, out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString()
            : null;
    }

    private static string? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return "Username and password are required.";
        }

        if (username.Trim().Length < 3)
        {
            return "Username must be at least 3 characters.";
        }

        if (password.Length < 6)
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
        return new GameMessage
        {
            Type = type,
            Payload = new { message }
        };
    }

    private static GameMessage ParseGameMessage(string json)
    {
        // Fast path: supports the baseline numeric enum format generated by GameMessage.ToJsonLine().
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
            // Fall back to document-friendly string types such as "CREATE_ROOM".
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!TryGetPropertyIgnoreCase(root, "type", out var typeElement))
        {
            throw new InvalidOperationException("Missing 'type' property.");
        }

        var type = ParseMessageType(typeElement);
        object? payload = null;
        if (TryGetPropertyIgnoreCase(root, "payload", out var payloadElement))
        {
            payload = payloadElement.Clone();
        }

        string? senderId = null;
        if (TryGetPropertyIgnoreCase(root, "senderId", out var senderElement))
        {
            senderId = senderElement.ValueKind == JsonValueKind.String ? senderElement.GetString() : senderElement.ToString();
        }

        var timestamp = DateTimeOffset.UtcNow;
        if (TryGetPropertyIgnoreCase(root, "timestamp", out var timestampElement))
        {
            timestamp = ParseTimestamp(timestampElement);
        }

        return new GameMessage
        {
            Type = type,
            Payload = payload,
            SenderId = senderId,
            Timestamp = timestamp
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

    private static DateTimeOffset ParseTimestamp(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var unix))
        {
            return unix > 9_999_999_999
                ? DateTimeOffset.FromUnixTimeMilliseconds(unix)
                : DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        return DateTimeOffset.UtcNow;
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
