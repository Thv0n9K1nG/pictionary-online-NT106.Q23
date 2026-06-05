using System.Collections.Concurrent;
using GameServer.Engine;
using GameServer.Rooms;
using GameServer.Services;
using Shared.Enums;
using Shared.Models;

namespace GameServer.Managers;

public sealed class RoomManager
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly GameEngine _gameEngine = new();

    public GameRoom CreateRoom(string sessionId, string hostPlayerId, string hostName, string ownerServerId)
    {
        var roomCode = GenerateRoomCode();
        var room = new GameRoom(roomCode, ownerServerId);

        room.AddPlayer(sessionId, new PlayerInfo(hostPlayerId, hostName, 0, true, true, false));

        _rooms[roomCode] = room;
        return room;
    }

    public bool TryGetRoom(string roomCode, out GameRoom? room)
    {
        return _rooms.TryGetValue(roomCode, out room);
    }

    public GameRoom RestoreRoom(RoomSnapshot snapshot, string ownerServerId)
    {
        var restored = GameRoom.FromSnapshot(snapshot, ownerServerId);
        _rooms[snapshot.RoomCode] = restored;
        return restored;
    }

    public GameRoom JoinRoom(string roomCode, string sessionId, string playerId, string playerName)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            throw new InvalidOperationException("Room not found.");
        }

        if (room.State != GameState.Waiting)
        {
            throw new InvalidOperationException("Room is not accepting players.");
        }

        room.AddPlayer(sessionId, new PlayerInfo(playerId, playerName, 0, false, true, false));
        return room;
    }

    public LeaveRoomResult LeaveRoom(string roomCode, string playerId)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            return new LeaveRoomResult(roomCode, true, null, []);
        }

        var result = room.RemovePlayer(playerId);
        if (result.RoomDeleted)
        {
            _rooms.TryRemove(roomCode, out _);
            return new LeaveRoomResult(roomCode, true, null, []);
        }

        return new LeaveRoomResult(roomCode, false, room, result.Players);
    }

    public async Task<RoundStartResult> ReadyAsync(string roomCode, string playerId, CancellationToken cancellationToken)
    {
        var room = GetRoom(roomCode);
        var words = await _gameEngine.GetWordOptionsAsync(cancellationToken);
        var players = room.StartSelectingWords(playerId, words);

        return new RoundStartResult(room, players, words, room.CurrentDrawerId!, room.CurrentDrawerSessionId!);
    }

    public async Task<WordSelectedResult> SelectWordAsync(
        string roomCode,
        string playerId,
        string word,
        CancellationToken cancellationToken = default)
    {
        var room = GetRoom(roomCode);
        room.SelectWord(playerId, word, _gameEngine);
        var currentWord = room.CurrentWord ?? string.Empty;

        return new WordSelectedResult(
            room,
            await _gameEngine.GenerateHintAsync(currentWord, cancellationToken),
            room.CurrentMaskedWord ?? _gameEngine.MaskWord(currentWord),
            room.GetSessionIdsExcept(playerId),
            GameEngine.RoundSeconds,
            room.RoundEndsAt ?? DateTimeOffset.UtcNow.AddSeconds(GameEngine.RoundSeconds),
            room.RoundVersion);
    }

    public GuessResult Guess(string roomCode, string playerId, string guess)
    {
        var room = GetRoom(roomCode);
        var result = room.ApplyGuess(playerId, guess, _gameEngine);

        return new GuessResult(room, result);
    }

    public DrawResult ApplyDraw(string roomCode, string playerId, DrawPayload payload)
    {
        var room = GetRoom(roomCode);
        room.ValidateDraw(playerId, payload);

        return new DrawResult(room.GetSessionIdsExcept(playerId));
    }

    public HintRevealResult? RevealHintLetter(string roomCode)
    {
        var room = GetRoom(roomCode);
        var maskedWord = room.RevealRandomMaskedLetter(_gameEngine, GameEngine.MinimumHiddenLettersBeforeReveal);
        if (string.IsNullOrWhiteSpace(maskedWord) || string.IsNullOrWhiteSpace(room.CurrentDrawerId))
        {
            return null;
        }

        return new HintRevealResult(room, maskedWord, room.GetSessionIdsExcept(room.CurrentDrawerId));
    }

    public GameRoom.RoundEndResult ExpireRound(string roomCode)
    {
        var room = GetRoom(roomCode);
        return room.ExpireRound(_gameEngine);
    }

    public int ActiveRoomCount => _rooms.Count;

    public int ActivePlayerCount => _rooms.Values.Sum(room => room.Players.Count);

    public IReadOnlyList<RoomInfo> GetWaitingRooms()
    {
        return _rooms.Values
            .Where(r => r.State == GameState.Waiting)
            .Select(ToRoomInfo)
            .ToList();
    }

    public IReadOnlyList<RoomSnapshot> CreateSnapshots(CheckpointService checkpointService)
    {
        return _rooms.Values
            .Select(checkpointService.CreateSnapshot)
            .ToList();
    }

    public static RoomInfo ToRoomInfo(GameRoom room)
    {
        var status = room.State switch
        {
            GameState.Waiting => RoomStatus.Waiting,
            GameState.SelectingWord or GameState.Drawing or GameState.RoundEnd => RoomStatus.Playing,
            GameState.GameOver => RoomStatus.Ended,
            _ => RoomStatus.Waiting
        };

        return new RoomInfo(room.RoomCode, room.HostName, room.Players.Count, 4, status, room.OwnerServerId);
    }

    private GameRoom GetRoom(string roomCode)
    {
        return _rooms.TryGetValue(roomCode, out var room)
            ? room
            : throw new InvalidOperationException("Room not found.");
    }

    private static string GenerateRoomCode()
    {
        return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }

    public sealed record RoundStartResult(
        GameRoom Room,
        IReadOnlyList<PlayerInfo> Players,
        IReadOnlyList<string> Words,
        string DrawerId,
        string DrawerSessionId);

    public sealed record WordSelectedResult(
        GameRoom Room,
        string Hint,
        string MaskedWord,
        IReadOnlyList<string> GuesserSessionIds,
        int RemainingSeconds,
        DateTimeOffset RoundEndsAt,
        int RoundVersion);

    public sealed record GuessResult(GameRoom Room, GameRoom.GuessResult Result);

    public sealed record DrawResult(IReadOnlyList<string> TargetSessionIds);

    public sealed record HintRevealResult(
        GameRoom Room,
        string MaskedWord,
        IReadOnlyList<string> GuesserSessionIds);

    public sealed record LeaveRoomResult(
        string RoomCode,
        bool RoomDeleted,
        GameRoom? Room,
        IReadOnlyList<PlayerInfo> Players);
}
