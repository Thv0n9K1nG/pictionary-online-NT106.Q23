using Client.State;
using Shared.Enums;
using Shared.Models;
using System.Text.Json;

namespace Client.Services;

public sealed class MessageDispatcher
{
    private readonly ClientState _state;
    
    public event Action<string>? SystemMessageReceived;
    public event Action? CorrectGuessReceived;
    public event Action<List<PlayerInfo>>? PlayerListUpdated;
    public event Action<int>? TimerUpdated;
    public event Action<string>? HintReceived;
    public event Action<List<string>>? WordOptionsReceived;
    public event Action<string>? SystemMessageReceived;
    public event Action? CorrectGuessReceived;
    public event Action? RoundEnded;
    public event Action? GameEnded;

    public MessageDispatcher(ClientState state)
    {
        _state = state;
    }

    public void Dispatch(GameMessage message)
    {
        switch (message.Type)
        {
            case MessageType.CorrectGuess:
                 CorrectGuessReceived?.Invoke();
                 break;

            case MessageType.Error:
            case MessageType.RoundEnd:
                 SystemMessageReceived?.Invoke(GetMessageString(message.Payload) ?? "");
                 break;
            case MessageType.PlayerList:
                HandlePlayerList(message);
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
                CorrectGuessReceived?.Invoke();
                break;

            case MessageType.RoundEnd:
                RoundEnded?.Invoke();
                break;

            case MessageType.GameEnd:
                GameEnded?.Invoke();
                break;
        }
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

        PlayerListUpdated?.Invoke(players);
    }

    private void HandleTimer(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
            return;

        if (!payload.TryGetProperty("remaining", out var remainingProp))
            return;

        int remaining = remainingProp.GetInt32();

        _state.LatestTimerValue = remaining;

        TimerUpdated?.Invoke(remaining);
    }

    private void HandleHint(GameMessage message)
    {
        if (message.Payload is not JsonElement payload)
            return;

        if (!payload.TryGetProperty("hint", out var hintProp))
            return;

        HintReceived?.Invoke(hintProp.GetString() ?? "");
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
            WordOptionsReceived?.Invoke(words);
        }
    }
}
