using Client.State;
using Shared.Enums;
using Shared.Models;
using System.Text.Json;

namespace Client.Services;
// Thêm class này vào để hứng JSON tự động
public class HintDto
{
    public string? Hint { get; set; }
    public string? MaskedWord { get; set; }
}

public sealed class MessageDispatcher
{
    private readonly ClientState _state;
    
    public event Action<string>? SystemMessageReceived;
    public event Action<string, int>? CorrectGuessReceived;

    public event Action<List<PlayerInfo>>? PlayerListUpdated;
    public event Action<int>? TimerUpdated;
    public event Action<string>? HintReceived;
    public event Action<List<string>>? WordOptionsReceived;
    public event Action<List<PlayerInfo>, bool>? RoundEnded;
    public event Action<MatchResult>? GameEnded;
    public event Action? GameplayStateChanged;


    public MessageDispatcher(ClientState state)
    {
        _state = state;
    }

    public void Dispatch(GameMessage message)
    {
        switch (message.Type)
        {
            case MessageType.RegisterSuccess:
                SystemMessageReceived?.Invoke(GetMessageString(message.Payload) ?? "Register successful.");
                break;

            case MessageType.RegisterFailed:
                _state.LastErrorMessage = GetMessageString(message.Payload) ?? "Register failed.";
                SystemMessageReceived?.Invoke(_state.LastErrorMessage);
                break;

            case MessageType.LoginSuccess:
                HandleLoginSuccess(message);
                break;

            case MessageType.LoginFailed:
                _state.LastErrorMessage = GetMessageString(message.Payload) ?? "Login failed.";
                SystemMessageReceived?.Invoke(_state.LastErrorMessage);
                break;

            case MessageType.RoomJoined:
                HandleRoomJoined(message);
                break;

            case MessageType.RoomList:
                HandleRoomList(message);
                break;

            case MessageType.PlayerList:
                HandlePlayerList(message);
                break;

            case MessageType.GameStart:
                HandleGameStart(message);
                break;
    
            case MessageType.TimerUpdate:
                HandleTimer(message);
                break;
    
            case MessageType.Hint:
                HandleHint(message);
                break;
    
            case MessageType.WordOptions:
                HandleWordOptions(message);
                break;
    
            case MessageType.CorrectGuess:
                HandleCorrectGuess(message);
                break;
    
            case MessageType.RoundEnd:
                HandleRoundEnd(message);
                break;
    
            case MessageType.GameEnd:
                HandleGameEnd(message);
                break;
    
            case MessageType.Error:
                _state.LastErrorMessage = GetMessageString(message.Payload) ?? "Error";
                SystemMessageReceived?.Invoke(_state.LastErrorMessage);
                break;
            // (Bạn tìm đến khối switch (message.Type) trong file này và gắn thêm case dưới đây)

            case MessageType.DrawData:
                if (message.Payload is System.Text.Json.JsonElement drawElement)
                {
                    try
                    {
                        // Cấu hình không phân biệt chữ hoa/thường để khớp JSON
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var rawText = drawElement.TryGetProperty("drawPayload", out var inner) ? inner.GetRawText() : drawElement.GetRawText();
                        var drawPayload = System.Text.Json.JsonSerializer.Deserialize<DrawPayload>(rawText, options);

                        if (drawPayload is not null)
                        {
                            _state.TriggerDrawDataReceived(drawPayload);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Error] Parse DrawData failed: {ex.Message}");
                    }
                }
                break;
        }
    }

    private void HandleLoginSuccess(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
            return;

        _state.SessionId = ReadString(payload, "sessionId");
        _state.PlayerId = ReadString(payload, "playerId") ?? ReadString(payload, "userId");
        _state.Username = ReadString(payload, "username") ?? _state.Username;
        _state.LastErrorMessage = null;

        SystemMessageReceived?.Invoke("Login successful.");
    }

    private void HandleRoomJoined(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
            return;

        _state.RoomCode = ReadString(payload, "roomCode");
        _state.CurrentGameState = GameState.Waiting;
        _state.LastErrorMessage = null;
        GameplayStateChanged?.Invoke();
    }

    private void HandleRoomList(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
            return;

        if (!payload.TryGetProperty("rooms", out var roomsProp))
            return;

        var rooms = JsonSerializer.Deserialize<List<RoomInfo>>(
            roomsProp.GetRawText(),
            GameMessage.JsonOptions);

        if (rooms is null)
            return;

        _state.RoomList.Clear();
        _state.RoomList.AddRange(rooms);
    }

    private void HandlePlayerList(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
            return;

        if (!payload.TryGetProperty("players", out var playersProp))
            return;

        var players =
            JsonSerializer.Deserialize<List<PlayerInfo>>(
                playersProp.GetRawText(),
                GameMessage.JsonOptions);

        if (players is null)
            return;

        _state.PlayerList.Clear();
        _state.PlayerList.AddRange(players);
        _state.IsDrawer = players.Any(player =>
            player.PlayerId == _state.PlayerId &&
            player.IsDrawer);
        _state.CurrentScore = players.FirstOrDefault(player => player.PlayerId == _state.PlayerId)?.Score ?? _state.CurrentScore;

        PlayerListUpdated?.Invoke(players);
        GameplayStateChanged?.Invoke();
    }

    private void HandleGameStart(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
            return;

        _state.CurrentGameState = GameState.SelectingWord;
        var drawerId = ReadString(payload, "drawerId");
        _state.IsDrawer = !string.IsNullOrWhiteSpace(drawerId) && drawerId == _state.PlayerId;

        SystemMessageReceived?.Invoke(_state.IsDrawer ? "You are drawing this round." : "Round started.");
        GameplayStateChanged?.Invoke();
    }

    private void HandleTimer(GameMessage message)
    {
        if (message.Payload is not System.Text.Json.JsonElement payload) return;

        int remaining = 0;
        try
        {
            // Quét và bóc tách mọi định dạng mà Backend có thể gửi về
            if (payload.ValueKind == System.Text.Json.JsonValueKind.Number) remaining = payload.GetInt32();
            else if (payload.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (payload.TryGetProperty("remainingSeconds", out var prop) ||
                    payload.TryGetProperty("remaining", out prop) ||
                    payload.TryGetProperty("seconds", out prop))
                {
                    remaining = prop.GetInt32();
                }
                else return;
            }
            else if (payload.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                if (int.TryParse(payload.GetString(), out var parsed)) remaining = parsed;
                else return;
            }
            else return;

            // Báo cho GameForm biết
            _state.LatestTimerValue = remaining;
            TimerUpdated?.Invoke(remaining);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lỗi giải mã Timer]: {ex.Message}");
        }
    }

    private void HandleHint(GameMessage message)
    {
        if (message.Payload is not System.Text.Json.JsonElement payload) return;

        try
        {
            string finalHint = "";

            // Trường hợp 1: Payload là một chuỗi đơn giản
            if (payload.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                finalHint = payload.GetString() ?? "";
            }
            // Trường hợp 2: Payload là Object JSON (Chứa maskedWord hoặc hint)
            else if (payload.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                string masked = "";
                string hint = "";

                // Quét toàn bộ JSON để tìm biến, bất chấp viết hoa hay viết thường
                foreach (var prop in payload.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "maskedWord", StringComparison.OrdinalIgnoreCase))
                        masked = prop.Value.GetString() ?? "";

                    if (string.Equals(prop.Name, "hint", StringComparison.OrdinalIgnoreCase))
                        hint = prop.Value.GetString() ?? "";
                }

                // Ưu tiên lấy MaskedWord (từ bị che), nếu không có mới xài Hint
                finalHint = !string.IsNullOrEmpty(masked) ? masked : hint;
            }

            // Gửi dữ liệu an toàn sang GameForm
            if (!string.IsNullOrEmpty(finalHint))
            {
                HintReceived?.Invoke(finalHint);
            }

            // Mở khóa giao diện
            _state.CurrentGameState = GameState.Drawing;
            _state.IsDrawer = false;
            GameplayStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Lỗi giải mã Hint]: {ex.Message}");
        }
    }

    private void HandleWordOptions(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
            return;

        if (!payload.TryGetProperty("words", out var wordsProp))
            return;

        var words =
            JsonSerializer.Deserialize<List<string>>(
                wordsProp.GetRawText(),
                GameMessage.JsonOptions);

        if (words is not null)
        {
            _state.CurrentGameState = GameState.SelectingWord;
            _state.IsDrawer = true;
            WordOptionsReceived?.Invoke(words);
            GameplayStateChanged?.Invoke();
        }
    }

    private void HandleCorrectGuess(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
        {
            CorrectGuessReceived?.Invoke("A player", 0);
            return;
        }

        var playerId = ReadString(payload, "playerId") ?? string.Empty;
        var playerName = _state.PlayerList.FirstOrDefault(player => player.PlayerId == playerId)?.DisplayName
            ?? (playerId == _state.PlayerId ? "You" : "A player");
        var scoreAwarded = ReadInt(payload, "scoreAwarded", 0);

        CorrectGuessReceived?.Invoke(playerName, scoreAwarded);
    }

    private void HandleRoundEnd(GameMessage message)
    {
        var players = _state.PlayerList.ToList();
        var gameEnded = false;

        if (message.Payload is JsonElement payload)
        {
            if (payload.TryGetProperty("players", out var playersProp))
            {
                players = JsonSerializer.Deserialize<List<PlayerInfo>>(
                    playersProp.GetRawText(),
                    GameMessage.JsonOptions) ?? players;
            }

            gameEnded = ReadBool(payload, "gameEnded", false);
        }

        _state.CurrentGameState = gameEnded ? GameState.GameOver : GameState.RoundEnd;
        _state.IsDrawer = false;
        _state.PlayerList.Clear();
        _state.PlayerList.AddRange(players);

        RoundEnded?.Invoke(players, gameEnded);
        PlayerListUpdated?.Invoke(players);
        SystemMessageReceived?.Invoke(gameEnded ? "Game ended." : "Round ended.");
        GameplayStateChanged?.Invoke();
    }

    private void HandleGameEnd(GameMessage message)
    {
        MatchResult? result = null;

        if (message.Payload is JsonElement payload)
        {
            result = payload.Deserialize<MatchResult>(GameMessage.JsonOptions);
        }

        if (result is null)
        {
            result = new MatchResult(
                _state.RoomCode ?? string.Empty,
                _state.PlayerList.OrderByDescending(player => player.Score).FirstOrDefault()?.PlayerId ?? string.Empty,
                _state.PlayerList.ToDictionary(player => player.PlayerId, player => player.Score),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
        }

        _state.CurrentGameState = GameState.GameOver;
        _state.IsDrawer = false;
        GameEnded?.Invoke(result);
        GameplayStateChanged?.Invoke();
    }

    private static string? GetMessageString(object? payload)
    {
        if (payload is null)
            return null;

        if (payload is string value)
            return value;

        if (payload is JsonElement element)
        {
            return ReadString(element, "message")
                ?? ReadString(element, "error")
                ?? (element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString());
        }

        var json = JsonSerializer.Serialize(payload, GameMessage.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return ReadString(doc.RootElement, "message")
            ?? ReadString(doc.RootElement, "error")
            ?? json;
    }

    private static string? ReadString(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static int ReadInt(JsonElement payload, string name, int defaultValue)
    {
        if (!payload.TryGetProperty(name, out var property))
            return defaultValue;

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(property.GetString(), out var number) => number,
            _ => defaultValue
        };
    }

    private static bool ReadBool(JsonElement payload, string name, bool defaultValue)
    {
        if (!payload.TryGetProperty(name, out var property))
            return defaultValue;

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }
}
