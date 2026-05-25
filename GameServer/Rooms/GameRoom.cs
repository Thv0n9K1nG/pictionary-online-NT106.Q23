using GameServer.Engine;
using Shared.Enums;
using Shared.Models;

namespace GameServer.Rooms;

public sealed class GameRoom
{
    private readonly List<PlayerInfo> _players = new();
    private readonly Dictionary<string, string> _sessionByPlayerId = new();
    private readonly HashSet<string> _correctGuessers = new();
    private readonly HashSet<int> _revealedLetterIndexes = new();
    private readonly Dictionary<string, int> _drawScores = new();
    private readonly Dictionary<string, int> _guessScores = new();
    private readonly Dictionary<string, int> _correctGuessCounts = new();
    private readonly List<DrawPayload> _canvasCommands = new();
    private readonly object _syncRoot = new();
    private int _drawerIndex = -1;

    public GameRoom(string roomCode, string ownerServerId)
    {
        RoomCode = roomCode;
        OwnerServerId = ownerServerId;
    }

    public string RoomCode { get; }
    public string OwnerServerId { get; private set; }
    public GameState State { get; private set; } = GameState.Waiting;
    public IReadOnlyList<PlayerInfo> Players
    {
        get
        {
            lock (_syncRoot)
            {
                return PlayersWithDrawerFlag();
            }
        }
    }

    public string HostName => Players.FirstOrDefault(p => p.IsHost)?.DisplayName ?? "Unknown";
    public string? CurrentDrawerId { get; private set; }
    public string? CurrentDrawerSessionId => CurrentDrawerId is not null && _sessionByPlayerId.TryGetValue(CurrentDrawerId, out var sessionId)
        ? sessionId
        : null;
    public string? CurrentWord { get; private set; }
    public string? CurrentMaskedWord { get; private set; }
    public IReadOnlyList<string> WordOptions { get; private set; } = [];
    public DateTimeOffset? RoundStartedAt { get; private set; }
    public DateTimeOffset? RoundEndsAt { get; private set; }
    public int CompletedRounds { get; private set; }
    public int RoundVersion { get; private set; }

    public void AddPlayer(string sessionId, PlayerInfo player)
    {
        lock (_syncRoot)
        {
            if (_players.Any(existing => existing.PlayerId == player.PlayerId))
            {
                _sessionByPlayerId[player.PlayerId] = sessionId;
                return;
            }

            if (_players.Count >= 4)
            {
                throw new InvalidOperationException("Room is full.");
            }

            _players.Add(player);
            _sessionByPlayerId[player.PlayerId] = sessionId;
        }
    }

    public void AddPlayer(PlayerInfo player)
    {
        AddPlayer(player.PlayerId, player);
    }

    public IReadOnlyList<string> GetSessionIdsExcept(string playerId)
    {
        lock (_syncRoot)
        {
            return _players
                .Where(player => player.PlayerId != playerId)
                .Select(player => _sessionByPlayerId.TryGetValue(player.PlayerId, out var sessionId) ? sessionId : null)
                .Where(sessionId => !string.IsNullOrWhiteSpace(sessionId))
                .Select(sessionId => sessionId!)
                .ToList();
        }
    }

    public IReadOnlyList<string> GetAllSessionIds()
    {
        lock (_syncRoot)
        {
            return _players
                .Select(player => _sessionByPlayerId.TryGetValue(player.PlayerId, out var sessionId) ? sessionId : null)
                .Where(sessionId => !string.IsNullOrWhiteSpace(sessionId))
                .Select(sessionId => sessionId!)
                .ToList();
        }
    }

    public RoomSnapshot ToSnapshot(long version)
    {
        lock (_syncRoot)
        {
            var remainingSeconds = RoundEndsAt is null
                ? 0
                : Math.Max(0, (int)Math.Ceiling((RoundEndsAt.Value - DateTimeOffset.UtcNow).TotalSeconds));

            // Stage 6 - A: capture enough room state for another GameServer to resume ownership.
            return new RoomSnapshot(
                RoomCode,
                State,
                PlayersWithDrawerFlag(),
                _players.ToDictionary(player => player.PlayerId, player => player.Score),
                CurrentDrawerId,
                CurrentMaskedWord,
                remainingSeconds,
                _correctGuessers.ToList(),
                _canvasCommands.ToList(),
                version,
                DateTimeOffset.UtcNow)
            {
                OwnerServerId = OwnerServerId,
                CurrentWord = CurrentWord,
                CompletedRounds = CompletedRounds,
                SessionIdsByPlayerId = new Dictionary<string, string>(_sessionByPlayerId)
            };
        }
    }

    public static GameRoom FromSnapshot(RoomSnapshot snapshot, string ownerServerId)
    {
        var room = new GameRoom(snapshot.RoomCode, ownerServerId);
        lock (room._syncRoot)
        {
            room._players.AddRange(snapshot.Players.Select(player =>
                player with { IsDrawer = player.PlayerId == snapshot.CurrentDrawerId }));

            foreach (var player in room._players)
            {
                var sessionId = snapshot.SessionIdsByPlayerId.TryGetValue(player.PlayerId, out var savedSessionId)
                    ? savedSessionId
                    : player.PlayerId;
                room._sessionByPlayerId[player.PlayerId] = sessionId;
            }

            room.State = snapshot.GameState;
            room.CurrentDrawerId = snapshot.CurrentDrawerId;
            room.CurrentWord = snapshot.CurrentWord;
            room.CurrentMaskedWord = snapshot.CurrentWordMasked;
            room.CompletedRounds = snapshot.CompletedRounds;
            room.RoundVersion = Math.Max(0, (int)Math.Min(int.MaxValue, snapshot.Version));
            room._drawerIndex = room._players.FindIndex(player => player.PlayerId == snapshot.CurrentDrawerId);
            room._correctGuessers.UnionWith(snapshot.GuessedPlayerIds);
            room._canvasCommands.AddRange(snapshot.CanvasCommands);

            if (snapshot.GameState == GameState.Drawing && snapshot.RemainingSeconds > 0)
            {
                room.RoundEndsAt = DateTimeOffset.UtcNow.AddSeconds(snapshot.RemainingSeconds);
                room.RoundStartedAt = room.RoundEndsAt.Value.AddSeconds(-GameEngine.RoundSeconds);
            }
        }

        return room;
    }

    public IReadOnlyList<PlayerInfo> StartSelectingWords(string hostPlayerId, IReadOnlyList<string> wordOptions)
    {
        lock (_syncRoot)
        {
            var host = _players.FirstOrDefault(player => player.IsHost);
            if (host is null || host.PlayerId != hostPlayerId)
            {
                throw new InvalidOperationException("Only host can start the game.");
            }

            if (_players.Count < 2)
            {
                throw new InvalidOperationException("At least 2 players are required.");
            }

            if (State != GameState.Waiting && State != GameState.RoundEnd)
            {
                throw new InvalidOperationException("Room is not ready to start a round.");
            }

            if (CompletedRounds >= _players.Count)
            {
                State = GameState.GameOver;
                throw new InvalidOperationException("Game is already over.");
            }

            _drawerIndex = _drawerIndex < 0
                ? Random.Shared.Next(_players.Count)
                : (_drawerIndex + 1) % _players.Count;

            CurrentDrawerId = _players[_drawerIndex].PlayerId;
            CurrentWord = null;
            CurrentMaskedWord = null;
            WordOptions = wordOptions.ToList();
            RoundStartedAt = null;
            RoundEndsAt = null;
            _canvasCommands.Clear();
            _correctGuessers.Clear();
            _revealedLetterIndexes.Clear();
            State = GameState.SelectingWord;

            return PlayersWithDrawerFlag();
        }
    }

    public void SelectWord(string playerId, string word, GameEngine engine)
    {
        lock (_syncRoot)
        {
            if (State != GameState.SelectingWord)
            {
                throw new InvalidOperationException("Room is not waiting for word selection.");
            }

            if (CurrentDrawerId != playerId)
            {
                throw new InvalidOperationException("Only the current drawer can select a word.");
            }

            if (!WordOptions.Any(option => string.Equals(option, word, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Selected word is not in the current word options.");
            }

            CurrentWord = WordOptions.First(option => string.Equals(option, word, StringComparison.OrdinalIgnoreCase));
            _revealedLetterIndexes.Clear();
            CurrentMaskedWord = engine.BuildMaskedWord(CurrentWord, _revealedLetterIndexes);
            RoundStartedAt = DateTimeOffset.UtcNow;
            RoundEndsAt = RoundStartedAt.Value.AddSeconds(GameEngine.RoundSeconds);
            RoundVersion++;
            _correctGuessers.Clear();
            State = GameState.Drawing;
        }
    }

    public string? RevealRandomMaskedLetter(GameEngine engine, int minimumHiddenLettersBeforeReveal)
    {
        lock (_syncRoot)
        {
            if (State != GameState.Drawing || string.IsNullOrWhiteSpace(CurrentWord))
            {
                return null;
            }

            var hiddenIndexes = Enumerable.Range(0, CurrentWord.Length)
                .Where(index => !char.IsWhiteSpace(CurrentWord[index]) && !_revealedLetterIndexes.Contains(index))
                .ToList();

            if (hiddenIndexes.Count <= minimumHiddenLettersBeforeReveal)
            {
                return null;
            }

            var selectedIndex = hiddenIndexes[Random.Shared.Next(hiddenIndexes.Count)];
            _revealedLetterIndexes.Add(selectedIndex);
            CurrentMaskedWord = engine.BuildMaskedWord(CurrentWord, _revealedLetterIndexes);
            return CurrentMaskedWord;
        }
    }

    public GuessResult ApplyGuess(string playerId, string guess, GameEngine engine)
    {
        lock (_syncRoot)
        {
            if (State != GameState.Drawing || string.IsNullOrWhiteSpace(CurrentWord) || RoundStartedAt is null)
            {
                throw new InvalidOperationException("No active drawing round.");
            }

            if (CurrentDrawerId == playerId)
            {
                throw new InvalidOperationException("Drawer cannot guess.");
            }

            if (_correctGuessers.Contains(playerId))
            {
                return new GuessResult(false, false, false, Players, 0);
            }

            if (!engine.IsCorrectGuess(guess, CurrentWord))
            {
                return new GuessResult(false, false, false, Players, 0);
            }

            var elapsedSeconds = (int)Math.Max(0, (DateTimeOffset.UtcNow - RoundStartedAt.Value).TotalSeconds);
            var score = engine.CalculateGuessScore(elapsedSeconds);
            AddScore(playerId, score);
            AddTrackedScore(_guessScores, playerId, score);
            AddTrackedScore(_correctGuessCounts, playerId, 1);
            _correctGuessers.Add(playerId);

            var allGuessersCorrect = _players
                .Where(player => player.PlayerId != CurrentDrawerId)
                .All(player => _correctGuessers.Contains(player.PlayerId));

            var ended = allGuessersCorrect;
            var gameEnded = false;
            if (ended)
            {
                gameEnded = EndRoundCore(engine);
            }

            return new GuessResult(true, ended, gameEnded, Players, score);
        }
    }

    public void ValidateDraw(string playerId, DrawPayload payload)
    {
        lock (_syncRoot)
        {
            // Member A: the GameServer remains authoritative for draw permission and round state.
            if (State != GameState.Drawing)
            {
                throw new InvalidOperationException("No active drawing round.");
            }

            if (CurrentDrawerId != playerId)
            {
                throw new InvalidOperationException("Only the current drawer can draw.");
            }

            _canvasCommands.Add(payload);
        }
    }

    public RoundEndResult ExpireRound(GameEngine engine)
    {
        lock (_syncRoot)
        {
            if (State != GameState.Drawing)
            {
                return new RoundEndResult(false, false, Players);
            }

            var gameEnded = EndRoundCore(engine);
            return new RoundEndResult(true, gameEnded, Players);
        }
    }

    public MatchResult ToMatchResult()
    {
        List<PlayerInfo> players;
        Dictionary<string, int> finalScores;
        PlayerInfo? winner;

        lock (_syncRoot)
        {
            players = PlayersWithDrawerFlag();
            finalScores = players.ToDictionary(player => player.PlayerId, player => player.Score);
            winner = players.OrderByDescending(player => player.Score).FirstOrDefault();
        }

        return new MatchResult(
            RoomCode,
            winner?.PlayerId ?? string.Empty,
            finalScores,
            RoundStartedAt ?? DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow)
        {
            Players = players.Select(player => new MatchPlayerResult(
                player.PlayerId,
                player.DisplayName,
                player.Score,
                _drawScores.GetValueOrDefault(player.PlayerId),
                _guessScores.GetValueOrDefault(player.PlayerId),
                _correctGuessCounts.GetValueOrDefault(player.PlayerId),
                player.PlayerId == winner?.PlayerId)).ToList()
        };
    }

    public void RestoreOwner(string ownerServerId)
    {
        OwnerServerId = ownerServerId;
    }

    private bool EndRoundCore(GameEngine engine)
    {
        if (CurrentDrawerId is not null)
        {
            var drawerScore = engine.CalculateDrawerScore(_correctGuessers.Count);
            AddScore(CurrentDrawerId, drawerScore);
            AddTrackedScore(_drawScores, CurrentDrawerId, drawerScore);
        }

        CompletedRounds++;
        State = CompletedRounds >= _players.Count ? GameState.GameOver : GameState.RoundEnd;
        RoundEndsAt = DateTimeOffset.UtcNow;
        return State == GameState.GameOver;
    }

    private void AddScore(string playerId, int score)
    {
        var index = _players.FindIndex(player => player.PlayerId == playerId);
        if (index >= 0)
        {
            _players[index] = _players[index] with { Score = _players[index].Score + score };
        }
    }

    private static void AddTrackedScore(Dictionary<string, int> scores, string playerId, int score)
    {
        scores[playerId] = scores.GetValueOrDefault(playerId) + score;
    }

    private List<PlayerInfo> PlayersWithDrawerFlag()
    {
        return _players
            .Select(player => player with { IsDrawer = player.PlayerId == CurrentDrawerId })
            .ToList();
    }

    public sealed record GuessResult(
        bool Correct,
        bool RoundEnded,
        bool GameEnded,
        IReadOnlyList<PlayerInfo> Players,
        int ScoreAwarded);

    public sealed record RoundEndResult(
        bool RoundEnded,
        bool GameEnded,
        IReadOnlyList<PlayerInfo> Players);
}
