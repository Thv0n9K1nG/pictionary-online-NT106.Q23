using Shared.Enums;

namespace Shared.Models;

public sealed record RoomSnapshot(
    string RoomCode,
    GameState GameState,
    IReadOnlyList<PlayerInfo> Players,
    Dictionary<string, int> Scores,
    string? CurrentDrawerId,
    string? CurrentWordMasked,
    int RemainingSeconds,
    IReadOnlyList<string> GuessedPlayerIds,
    IReadOnlyList<DrawPayload> CanvasCommands,
    long Version,
    DateTimeOffset UpdatedAt
);
