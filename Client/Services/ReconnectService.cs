namespace Client.Services;

using Shared.Enums;
using Shared.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class ReconnectService
{
    private readonly SocketService _socketService;

    // Sự kiện để UI (GameForm/LobbyForm) lắng nghe và render lại khi kết nối thành công
    public event Action? OnRoomStateRestored;

    public ReconnectService(SocketService socketService)
    {
        _socketService = socketService;
        
        // Lắng nghe tín hiệu phục hồi phòng từ Gateway
        _socketService.MessageReceived += OnMessageReceived;
    }

    public async Task TryReconnectAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // 1. Mất kết nối -> reconnect Gateway
        if (!_socketService.IsConnected)
        {
            // Cấu hình Host/Port bạn có thể lấy từ ClientState hoặc Constant, ở đây mình để mặc định
            await _socketService.ConnectAsync("127.0.0.1", 5000, cancellationToken);
        }

        // 2. Gửi RECONNECT(sessionId) trực tiếp
        var reconnectMessage = new GameMessage
        {
            Type = MessageType.Reconnect,
            Payload = new { sessionId }
        };
        
        await _socketService.SendAsync(reconnectMessage, cancellationToken);
    }

    private void OnMessageReceived(object? sender, GameMessage message)
    {
        // 3. Nhận roomState (RoomRecovered) -> render lại UI
        if (message.Type == MessageType.RoomRecovered)
        {
            OnRoomStateRestored?.Invoke();
        }
    }
}
