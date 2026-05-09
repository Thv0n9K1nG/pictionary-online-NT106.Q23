using Client.State;
using Shared.Enums;
using Shared.Models;
using System.Text.Json;
using System.Windows.Forms;

namespace Client.Services;

public sealed class MessageDispatcher
{
    private readonly ClientState _state;

    public MessageDispatcher(ClientState state)
    {
        _state = state;
    }

    public void Dispatch(GameMessage message)
    {
        switch (message.Type)
        {
            case MessageType.LoginSuccess:
                // Trích xuất dữ liệu từ Payload lưu vào ClientState
                if (message.Payload is JsonElement loginPayload)
                {
                    if (loginPayload.TryGetProperty("sessionId", out var sessionProp))
                        _state.SessionId = sessionProp.GetString();

                    if (loginPayload.TryGetProperty("playerId", out var playerProp))
                        _state.PlayerId = playerProp.GetString();
                }
                MessageBox.Show("Đăng nhập thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;

            case MessageType.LoginFailed:
                string loginError = GetMessageString(message.Payload) ?? "Sai tài khoản hoặc mật khẩu.";
                MessageBox.Show($"Đăng nhập thất bại: {loginError}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;

            case MessageType.RegisterSuccess:
                MessageBox.Show("Đăng ký thành công! Bạn có thể đăng nhập ngay bây giờ.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;

            case MessageType.RegisterFailed:
                string regError = GetMessageString(message.Payload) ?? "Tên đăng nhập đã tồn tại hoặc không hợp lệ.";
                MessageBox.Show($"Đăng ký thất bại: {regError}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;

            case MessageType.RoomJoined:
                if (message.Payload is JsonElement roomJoinedPayload)
                {
                    if (roomJoinedPayload.TryGetProperty("roomCode", out var roomCodeProp))
                    {
                        _state.RoomCode = roomCodeProp.GetString();
                    }
                }
                break;

            case MessageType.PlayerList:
                if (message.Payload is JsonElement playerListPayload &&
                    playerListPayload.TryGetProperty("players", out var playersProp))
                {
                    var players = JsonSerializer.Deserialize<List<PlayerInfo>>(playersProp.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _state.PlayerList.Clear();
                    if (players is not null)
                    {
                        _state.PlayerList.AddRange(players);
                    }
                }
                break;

            case MessageType.RoomList:
                if (message.Payload is JsonElement roomListPayload &&
                    roomListPayload.TryGetProperty("rooms", out var roomsProp))
                {
                    var rooms = JsonSerializer.Deserialize<List<RoomInfo>>(roomsProp.GetRawText(), new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _state.RoomList.Clear();
                    if (rooms is not null)
                    {
                        _state.RoomList.AddRange(rooms);
                    }
                }
                break;

            case MessageType.DrawData:
            case MessageType.Error:
                _state.LastErrorMessage = GetMessageString(message.Payload);
                // TODO: Update state and UI through UiThreadDispatcher.
                break;

            default:
                break;
        }
    }

    // Hàm phụ trợ dùng để trích xuất nội dung lỗi từ object Payload
    private string? GetMessageString(object? payload)
    {
        if (payload is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("message", out var msgProp))
                return msgProp.GetString();
        }
        return payload?.ToString();
    }
}
