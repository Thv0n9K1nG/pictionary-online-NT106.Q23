using System.Net.Sockets;
using System.Text.Json;
using GameServer.Engine;
using GameServer.Managers;
using GameServer.Rooms;
using GameServer.Services;
using Shared.Enums;
using Shared.Models;
using Shared.Protocol;

namespace GameServer.Handlers;

public sealed class GatewayHandler
{
    private readonly string _serverId;
    private readonly RoomManager _roomManager;
    private readonly CheckpointService _checkpointService;
    private readonly Func<NetworkStream, InternalMessageType, object, CancellationToken, Task> _sendInternalAsync;

    public GatewayHandler(
        string serverId,
        RoomManager roomManager,
        CheckpointService checkpointService,
        Func<NetworkStream, InternalMessageType, object, CancellationToken, Task> sendInternalAsync)
    {
        _serverId = serverId;
        _roomManager = roomManager;
        _checkpointService = checkpointService;
        _sendInternalAsync = sendInternalAsync;
    }

    public async Task HandleInternalMessageAsync(
        JsonElement envelope,
        NetworkStream stream,
        CancellationToken cancellationToken = default)
    {
        var type = ParseInternalType(ReadProperty(envelope, "type"));
        var payload = TryGetPropertyIgnoreCase(envelope, "payload", out var payloadElement)
            ? payloadElement
            : default;

        switch (type)
        {
            case InternalMessageType.CreateRoomOnNode:
                await HandleCreateRoomOnNodeAsync(payload, stream, cancellationToken);
                break;
            case InternalMessageType.ForwardClientMessage:
                await HandleForwardClientMessageAsync(payload, stream, cancellationToken);
                break;
            case InternalMessageType.RestoreRoomFromCheckpoint:
                await HandleRestoreRoomFromCheckpointAsync(payload, stream, cancellationToken);
                break;
            case InternalMessageType.LeaveRoom:
                await HandleLeaveRoomAsync(payload, stream, cancellationToken);
                break;
            default:
                Console.WriteLine($"[GameServer:{_serverId}] Internal message {type} is not handled.");
                break;
        }
    }

    private async Task HandleCreateRoomOnNodeAsync(JsonElement payload, NetworkStream stream, CancellationToken cancellationToken)
    {
        var requestId = ReadString(payload, "requestId");
        try
        {
            var sessionId = ReadString(payload, "sessionId") ?? throw new InvalidOperationException("Missing sessionId.");
            var playerId = ReadString(payload, "playerId") ?? throw new InvalidOperationException("Missing playerId.");
            var playerName = ReadString(payload, "playerName") ?? playerId;

            var room = _roomManager.CreateRoom(sessionId, playerId, playerName, _serverId);
            await SendRoomResponseAsync(requestId, room, stream, cancellationToken);
            await SendCheckpointAsync(room, stream, cancellationToken);

            Console.WriteLine($"[GameServer:{_serverId}] Created room {room.RoomCode}.");
        }
        catch (Exception ex)
        {
            await SendFailureAsync(requestId, ex.Message, stream, cancellationToken);
        }
    }

    private async Task HandleForwardClientMessageAsync(JsonElement payload, NetworkStream stream, CancellationToken cancellationToken)
    {
        var requestId = ReadString(payload, "requestId");
        try
        {
            var roomCode = ReadString(payload, "roomCode")?.Trim().ToUpperInvariant()
                ?? throw new InvalidOperationException("Missing roomCode.");
            var sessionId = ReadString(payload, "sessionId") ?? throw new InvalidOperationException("Missing sessionId.");
            var playerId = ReadString(payload, "playerId") ?? throw new InvalidOperationException("Missing playerId.");
            var playerName = ReadString(payload, "playerName") ?? playerId;
            var innerMessage = ReadGameMessage(payload);

            switch (innerMessage.Type)
            {
                case MessageType.Join:
                    await HandleJoinAsync(requestId, roomCode, sessionId, playerId, playerName, stream, cancellationToken);
                    break;
                case MessageType.Ready:
                    await HandleReadyAsync(requestId, roomCode, playerId, stream, cancellationToken);
                    break;
                case MessageType.SelectWord:
                    await HandleSelectWordAsync(requestId, roomCode, playerId, innerMessage, stream, cancellationToken);
                    break;
                case MessageType.Draw:
                    await HandleDrawAsync(requestId, roomCode, playerId, innerMessage, stream, cancellationToken);
                    break;
                case MessageType.Guess:
                    await HandleGuessAsync(requestId, roomCode, playerId, innerMessage, stream, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"{innerMessage.Type} is not supported by the GameServer yet.");
            }
        }
        catch (Exception ex)
        {
            await SendFailureAsync(requestId, ex.Message, stream, cancellationToken);
        }
    }

    private async Task HandleJoinAsync(
        string? requestId,
        string roomCode,
        string sessionId,
        string playerId,
        string playerName,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var room = _roomManager.JoinRoom(roomCode, sessionId, playerId, playerName);
        await SendRoomResponseAsync(requestId, room, stream, cancellationToken);
        await SendCheckpointAsync(room, stream, cancellationToken);
        Console.WriteLine($"[GameServer:{_serverId}] Player {playerId} joined room {room.RoomCode}.");
    }

    private async Task HandleLeaveRoomAsync(JsonElement payload, NetworkStream stream, CancellationToken cancellationToken)
    {
        var requestId = ReadString(payload, "requestId");
        try
        {
            var roomCode = ReadString(payload, "roomCode")?.Trim().ToUpperInvariant()
                ?? throw new InvalidOperationException("Missing roomCode.");
            var playerId = ReadString(payload, "playerId") ?? throw new InvalidOperationException("Missing playerId.");

            var result = _roomManager.LeaveRoom(roomCode, playerId);
            var roomInfo = result.Room is null ? null : RoomManager.ToRoomInfo(result.Room);

            await _sendInternalAsync(stream, InternalMessageType.ServerEvent, new
            {
                requestId,
                success = true,
                roomCode,
                roomDeleted = result.RoomDeleted,
                roomInfo,
                players = result.Players
            }, cancellationToken);

            if (result.Room is not null)
            {
                await SendCheckpointAsync(result.Room, stream, cancellationToken);
            }

            Console.WriteLine($"[GameServer:{_serverId}] Player {playerId} left room {roomCode}.");
        }
        catch (Exception ex)
        {
            await SendFailureAsync(requestId, ex.Message, stream, cancellationToken);
        }
    }

    private async Task HandleReadyAsync(
        string? requestId,
        string roomCode,
        string playerId,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var result = await _roomManager.ReadyAsync(roomCode, playerId, cancellationToken);
        await SendSuccessAckAsync(requestId, stream, cancellationToken);
        await SendCheckpointAsync(result.Room, stream, cancellationToken);

        await SendRoomEventAsync(roomCode, new GameMessage
        {
            Type = MessageType.GameStart,
            Payload = new
            {
                roomCode,
                state = GameState.SelectingWord,
                drawerId = result.DrawerId,
                round = result.Room.CompletedRounds + 1
            }
        }, stream, cancellationToken);

        await SendRoomEventAsync(roomCode, new GameMessage
        {
            Type = MessageType.PlayerList,
            Payload = new { roomCode, players = result.Players }
        }, stream, cancellationToken);

        await SendTargetedRoomEventAsync(roomCode, [result.DrawerSessionId], new GameMessage
        {
            Type = MessageType.WordOptions,
            Payload = new { roomCode, drawerId = result.DrawerId, words = result.Words }
        }, stream, cancellationToken);
    }

    private async Task HandleSelectWordAsync(
        string? requestId,
        string roomCode,
        string playerId,
        GameMessage message,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var selectedWord = ReadStringFromPayload(message.Payload, "word")
            ?? ReadStringFromPayload(message.Payload, "selectedWord")
            ?? throw new InvalidOperationException("Missing selected word.");

        var result = await _roomManager.SelectWordAsync(roomCode, playerId, selectedWord, cancellationToken);
        await SendSuccessAckAsync(requestId, stream, cancellationToken);
        await SendCheckpointAsync(result.Room, stream, cancellationToken);

        await SendTargetedRoomEventAsync(roomCode, result.GuesserSessionIds, new GameMessage
        {
            Type = MessageType.Hint,
            Payload = new
            {
                roomCode,
                hint = result.Hint,
                maskedWord = result.MaskedWord,
                drawerId = playerId,
                remainingSeconds = result.RemainingSeconds,
                roundEndsAt = result.RoundEndsAt
            }
        }, stream, cancellationToken);

        await SendRoomEventAsync(roomCode, new GameMessage
        {
            Type = MessageType.TimerUpdate,
            Payload = new { roomCode, remainingSeconds = result.RemainingSeconds }
        }, stream, cancellationToken);

        _ = Task.Run(() => RunRoundLoopAsync(roomCode, result.RoundVersion, stream), CancellationToken.None);
    }

    private async Task HandleDrawAsync(
        string? requestId,
        string roomCode,
        string playerId,
        GameMessage message,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var payload = ReadDrawPayload(message.Payload)
            ?? throw new InvalidOperationException("Missing draw payload.");

        var result = _roomManager.ApplyDraw(roomCode, playerId, payload);
        await SendSuccessAckAsync(requestId, stream, cancellationToken);
        if (_roomManager.TryGetRoom(roomCode, out var checkpointRoom) && checkpointRoom is not null)
        {
            await SendCheckpointAsync(checkpointRoom, stream, cancellationToken);
        }

        // Member A: broadcast accepted drawing commands only to guessers, not back to the drawer.
        await SendTargetedRoomEventAsync(roomCode, result.TargetSessionIds, new GameMessage
        {
            Type = MessageType.DrawData,
            Payload = new { roomCode, drawPayload = payload }
        }, stream, cancellationToken);
    }

    private async Task HandleGuessAsync(
        string? requestId,
        string roomCode,
        string playerId,
        GameMessage message,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var guess = ReadStringFromPayload(message.Payload, "guess")
            ?? ReadStringFromPayload(message.Payload, "text")
            ?? string.Empty;

        var result = _roomManager.Guess(roomCode, playerId, guess);
        await SendSuccessAckAsync(requestId, stream, cancellationToken);
        await SendCheckpointAsync(result.Room, stream, cancellationToken);

        if (!result.Result.Correct)
        {
            return;
        }

        await SendRoomEventAsync(roomCode, new GameMessage
        {
            Type = MessageType.CorrectGuess,
            Payload = new
            {
                roomCode,
                playerId,
                scoreAwarded = result.Result.ScoreAwarded
            }
        }, stream, cancellationToken);

        await SendRoomEventAsync(roomCode, new GameMessage
        {
            Type = MessageType.PlayerList,
            Payload = new { roomCode, players = result.Result.Players }
        }, stream, cancellationToken);

        if (result.Result.RoundEnded)
        {
            await SendRoundEndEventsAsync(roomCode, result.Result.Players, result.Result.GameEnded, stream, cancellationToken);
        }
    }

    private async Task HandleRestoreRoomFromCheckpointAsync(
        JsonElement payload,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var requestId = ReadString(payload, "requestId");
        try
        {
            var snapshot = payload.Deserialize<RoomSnapshot>(GameMessage.JsonOptions)
                ?? throw new InvalidOperationException("Invalid room checkpoint.");
            _checkpointService.ObserveVersion(snapshot.RoomCode, snapshot.Version);
            var room = _roomManager.RestoreRoom(snapshot, _serverId);

            // Stage 6 - A: acknowledge restore through the existing request/response channel.
            await _sendInternalAsync(stream, InternalMessageType.ServerEvent, new
            {
                requestId,
                success = true,
                roomCode = room.RoomCode,
                ownerServerId = _serverId,
                roomInfo = RoomManager.ToRoomInfo(room)
            }, cancellationToken);

            await SendCheckpointAsync(room, stream, cancellationToken);
            Console.WriteLine($"[GameServer:{_serverId}] Restored room {room.RoomCode} from checkpoint.");
        }
        catch (Exception ex)
        {
            await SendFailureAsync(requestId, ex.Message, stream, cancellationToken);
        }
    }

    private async Task SendRoundEndEventsAsync(
        string roomCode,
        IReadOnlyList<PlayerInfo> players,
        bool gameEnded,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        await SendRoomEventAsync(roomCode, new GameMessage
        {
            Type = MessageType.RoundEnd,
            Payload = new { roomCode, players, gameEnded }
        }, stream, cancellationToken);

        if (gameEnded && _roomManager.TryGetRoom(roomCode, out var room) && room is not null)
        {
            var matchResult = room.ToMatchResult();
            await SendRoomEventAsync(roomCode, new GameMessage
            {
                Type = MessageType.GameEnd,
                Payload = matchResult
            }, stream, cancellationToken);

            var reporter = new MatchResultReporter((type, payload, token) =>
                _sendInternalAsync(stream, type, payload, token));
            await reporter.ReportAsync(matchResult, cancellationToken);
        }

        if (_roomManager.TryGetRoom(roomCode, out var checkpointRoom) && checkpointRoom is not null)
        {
            await SendCheckpointAsync(checkpointRoom, stream, cancellationToken);
        }
    }

    private async Task RunRoundLoopAsync(string roomCode, int roundVersion, NetworkStream stream)
    {
        try
        {
            for (var elapsedSeconds = 1; elapsedSeconds <= GameEngine.RoundSeconds; elapsedSeconds++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);

                if (!_roomManager.TryGetRoom(roomCode, out var room) ||
                    room is null ||
                    room.State != GameState.Drawing ||
                    room.RoundVersion != roundVersion)
                {
                    return;
                }

                var remainingSeconds = Math.Max(0, GameEngine.RoundSeconds - elapsedSeconds);
                await SendRoomEventAsync(roomCode, new GameMessage
                {
                    Type = MessageType.TimerUpdate,
                    Payload = new { roomCode, remainingSeconds }
                }, stream, CancellationToken.None);

                if (elapsedSeconds < GameEngine.RoundSeconds &&
                    elapsedSeconds % GameEngine.HintRevealIntervalSeconds == 0)
                {
                    var reveal = _roomManager.RevealHintLetter(roomCode);
                    if (reveal is not null)
                    {
                        await SendTargetedRoomEventAsync(roomCode, reveal.GuesserSessionIds, new GameMessage
                        {
                            Type = MessageType.Hint,
                            Payload = new
                            {
                                roomCode,
                                maskedWord = reveal.MaskedWord,
                                remainingSeconds,
                                drawerId = reveal.Room.CurrentDrawerId
                            }
                        }, stream, CancellationToken.None);
                    }
                }
            }

            var endResult = _roomManager.ExpireRound(roomCode);
            if (endResult.RoundEnded)
            {
                await SendRoundEndEventsAsync(roomCode, endResult.Players, endResult.GameEnded, stream, CancellationToken.None);
            }
        }
        catch
        {
            // Round timer and hint sync stop when the internal connection is gone.
        }
    }

    private Task SendRoomResponseAsync(
        string? requestId,
        GameRoom room,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var roomInfo = RoomManager.ToRoomInfo(room);
        var payload = new
        {
            requestId,
            success = true,
            roomCode = room.RoomCode,
            roomInfo,
            players = room.Players
        };

        return _sendInternalAsync(stream, InternalMessageType.ServerEvent, payload, cancellationToken);
    }

    private Task SendCheckpointAsync(GameRoom room, NetworkStream stream, CancellationToken cancellationToken)
    {
        var snapshot = _checkpointService.CreateSnapshot(room);
        return _sendInternalAsync(stream, InternalMessageType.RoomCheckpoint, snapshot, cancellationToken);
    }

    private Task SendSuccessAckAsync(string? requestId, NetworkStream stream, CancellationToken cancellationToken)
    {
        return _sendInternalAsync(stream, InternalMessageType.ServerEvent, new
        {
            requestId,
            success = true
        }, cancellationToken);
    }

    private Task SendFailureAsync(string? requestId, string error, NetworkStream stream, CancellationToken cancellationToken)
    {
        var payload = new
        {
            requestId,
            success = false,
            error
        };

        return _sendInternalAsync(stream, InternalMessageType.ServerEvent, payload, cancellationToken);
    }

    private Task SendRoomEventAsync(
        string roomCode,
        GameMessage message,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        return _sendInternalAsync(stream, InternalMessageType.ServerEvent, new
        {
            roomCode,
            innerMessage = message
        }, cancellationToken);
    }

    private Task SendTargetedRoomEventAsync(
        string roomCode,
        IReadOnlyList<string> targetSessionIds,
        GameMessage message,
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        return _sendInternalAsync(stream, InternalMessageType.ServerEvent, new
        {
            roomCode,
            targetSessionIds,
            innerMessage = message
        }, cancellationToken);
    }

    private static GameMessage ReadGameMessage(JsonElement payload)
    {
        if (!TryGetPropertyIgnoreCase(payload, "innerMessage", out var innerMessageElement))
        {
            return new GameMessage { Type = MessageType.Join };
        }

        return innerMessageElement.Deserialize<GameMessage>(GameMessage.JsonOptions)
            ?? throw new InvalidOperationException("Invalid inner message.");
    }

    private static DrawPayload? ReadDrawPayload(object? payload)
    {
        if (payload is null)
        {
            return null;
        }

        if (payload is JsonElement element)
        {
            if (TryGetPropertyIgnoreCase(element, "drawPayload", out var drawPayloadElement))
            {
                return drawPayloadElement.Deserialize<DrawPayload>(GameMessage.JsonOptions);
            }

            return element.Deserialize<DrawPayload>(GameMessage.JsonOptions);
        }

        var json = JsonSerializer.Serialize(payload, GameMessage.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return ReadDrawPayload(doc.RootElement);
    }

    private static JsonElement ReadProperty(JsonElement element, string name)
    {
        return TryGetPropertyIgnoreCase(element, name, out var value) ? value : default;
    }

    private static string? ReadString(JsonElement payload, string name)
    {
        if (TryGetPropertyIgnoreCase(payload, name, out var value))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        return null;
    }

    private static string? ReadStringFromPayload(object? payload, string name)
    {
        if (payload is JsonElement element)
        {
            return ReadString(element, name);
        }

        if (payload is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(payload, GameMessage.JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return ReadString(doc.RootElement, name);
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
