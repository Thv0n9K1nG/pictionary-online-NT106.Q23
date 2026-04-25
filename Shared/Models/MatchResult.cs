namespace Shared.Models;

public sealed record MatchResult(
    string RoomCode,
    string WinnerId,
    IReadOnlyDictionary<string, int> FinalScores,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt
);
