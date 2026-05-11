using System.Collections.Concurrent;
using GameServer.Engine;
using GameServer.Rooms;
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

        room.AddPlayer(sessionId, new PlayerInfo(hostPlayerId, hostName, 0, true, true));

        _rooms[roomCode] = room;
        return room;
    }

    public bool TryGetRoom(string roomCode, out GameRoom? room)
    {
        return _rooms.TryGetValue(roomCode, out room);
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

        room.AddPlayer(sessionId, new PlayerInfo(playerId, playerName, 0, false, true));
        return room;
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
        room.SelectWord(playerId, word);
        var currentWord = room.CurrentWord ?? string.Empty;

        return new WordSelectedResult(
            room,
            await _gameEngine.GenerateHintAsync(currentWord, cancellationToken),
            _gameEngine.MaskWord(currentWord),
            room.GetSessionIdsExcept(playerId),
            GameEngine.RoundSeconds,
            room.RoundEndsAt ?? DateTimeOffset.UtcNow.AddSeconds(GameEngine.RoundSeconds));
    }

    public GuessResult Guess(string roomCode, string playerId, string guess)
    {
        var room = GetRoom(roomCode);
        var result = room.ApplyGuess(playerId, guess, _gameEngine);

        return new GuessResult(room, result);
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
        DateTimeOffset RoundEndsAt);

    public sealed record GuessResult(GameRoom Room, GameRoom.GuessResult Result);
}
