using System.Text;
using System.Text.Json;
using Gateway.Managers;
using Gateway.Services;
using Shared.Enums;
using Shared.Models;

namespace Gateway.Handlers;

/// <summary>
/// Quản lý kết nối của một Client sau khi đã thiết lập TLS.
/// Thực hiện xác thực (Register/Login) và quản lý phiên làm việc.
/// </summary>
public sealed class ClientHandler
{
    private readonly Stream _stream;
    private readonly AuthService? _authService;
    private readonly SessionDirectory? _sessionDirectory;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

    public ClientHandler(Stream stream, AuthService authService, SessionDirectory sessionDirectory)
    {
        _stream = stream;
        _authService = authService;
        _sessionDirectory = sessionDirectory;
    }

    /// <summary>
    /// Gửi gói tin JSON về phía Client.
    /// </summary>
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

    /// <summary>
    /// Vòng lặp chính đọc dữ liệu từ Socket.
    /// </summary>
    public async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        var readBuffer = new byte[4096];
        var textBuffer = new StringBuilder();

        Console.WriteLine($"[Gateway][Client:{ShortId}] Bắt đầu xử lý kết nối.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bytesRead = await _stream.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken);
                if (bytesRead == 0)
                {
                    Console.WriteLine($"[Gateway][Client:{ShortId}] Client đã ngắt kết nối.");
                    break;
                }

                textBuffer.Append(Encoding.UTF8.GetString(readBuffer, 0, bytesRead));
                await ProcessBufferedLinesAsync(textBuffer, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gateway][Client:{ShortId}] Lỗi đọc stream: {ex.Message}");
                break;
            }
        }
    }

    private async Task ProcessBufferedLinesAsync(StringBuilder textBuffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            var current = textBuffer.ToString();
            var newlineIndex = current.IndexOf('\n');
            if (newlineIndex < 0) return;

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
            Console.WriteLine($"[Gateway][Client:{ShortId}] Nhận tin nhắn: {message.Type}");

            switch (message.Type)
            {
                case MessageType.Register:
                    await HandleRegisterAsync(message, cancellationToken);
                    break;

                case MessageType.Login:
                    await HandleLoginAsync(message, cancellationToken);
                    break;

                default:
                    // Trong các giai đoạn sau, các tin nhắn gameplay (vẽ, chat) 
                    // sẽ được chuyển tiếp qua ProxyRouter tại đây.
                    Console.WriteLine($"[Gateway][Client:{ShortId}] Loại tin nhắn {message.Type} chưa được hỗ trợ xử lý.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gateway][Client:{ShortId}] Lỗi xử lý gói tin: {ex.Message}");
        }
    }

    private async Task HandleRegisterAsync(GameMessage message, CancellationToken cancellationToken)
    {
        if (_authService is null) return;

        var (username, password) = ReadCredentials(message.Payload);

        // 1. Validate dữ liệu đầu vào
        var validationError = ValidateCredentials(username, password);
        if (validationError is not null)
        {
            await SendAsync(CreateError(MessageType.RegisterFailed, validationError), cancellationToken);
            return;
        }

        try
        {
            // 2. Gọi AuthService thực hiện Hash + Insert DB
            var userId = await _authService.RegisterAsync(username, password, cancellationToken);
            Console.WriteLine($"[Gateway][Client:{ShortId}] Đăng ký thành công: {username}");

            // 3. Trả về REGISTER_SUCCESS
            await SendAsync(new GameMessage
            {
                Type = MessageType.RegisterSuccess,
                Payload = new { userId, playerId = userId, username }
            }, cancellationToken);
        }
        catch (Exception)
        {
            // Thường là lỗi UNIQUE constraint do trùng username trong DB
            await SendAsync(CreateError(MessageType.RegisterFailed, "Tên đăng nhập đã tồn tại hoặc dữ liệu không hợp lệ."), cancellationToken);
        }
    }

    private async Task HandleLoginAsync(GameMessage message, CancellationToken cancellationToken)
    {
        if (_authService is null || _sessionDirectory is null) return;

        var (username, password) = ReadCredentials(message.Payload);

        // 1. Gọi AuthService để Verify BCrypt và tạo SessionId
        var sessionId = await _authService.LoginAsync(username, password, cancellationToken);

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            Console.WriteLine($"[Gateway][Client:{ShortId}] Đăng nhập thất bại: {username}");
            await SendAsync(CreateError(MessageType.LoginFailed, "Tài khoản hoặc mật khẩu không chính xác."), cancellationToken);
            return;
        }

        var userId = ExtractUserIdFromSessionId(sessionId);

        // 2. Lưu vào SessionDirectory (RAM) để quản lý các gói tin tiếp theo
        var session = new SessionInfo(
            SessionId: sessionId,
            UserId: userId,
            PlayerId: userId,
            RoomCode: null,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(4)); // Session mặc định hết hạn sau 4h

        _sessionDirectory.Set(session);
        Console.WriteLine($"[Gateway][Client:{ShortId}] Đăng nhập thành công: {username}");

        // 3. Trả về LOGIN_SUCCESS kèm sessionId và thông tin định danh
        await SendAsync(new GameMessage
        {
            Type = MessageType.LoginSuccess,
            Payload = new { sessionId, userId, playerId = userId, username }
        }, cancellationToken);
    }

    #region Helpers

    private string ShortId => ConnectionId.Length <= 8 ? ConnectionId : ConnectionId[..8];

    private static (string Username, string Password) ReadCredentials(object? payload)
    {
        if (payload is JsonElement element)
        {
            return (
                ReadStringProperty(element, "username") ?? string.Empty,
                ReadStringProperty(element, "password") ?? string.Empty);
        }
        return (string.Empty, string.Empty);
    }

    private static string? ReadStringProperty(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
            }
        }
        return null;
    }

    private static string? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            return "Tên đăng nhập phải có ít nhất 3 ký tự.";
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return "Mật khẩu phải có ít nhất 6 ký tự.";
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
        var msg = GameMessage.FromJson(json);
        return msg ?? throw new InvalidOperationException("Không thể giải mã gói tin JSON.");
    }

    #endregion
}