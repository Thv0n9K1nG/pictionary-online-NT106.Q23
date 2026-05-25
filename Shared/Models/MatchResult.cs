namespace Shared.Models;

public sealed record MatchResult(
    string RoomCode,
    string WinnerId,
    IReadOnlyDictionary<string, int> FinalScores,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt)
{
    public IReadOnlyList<MatchPlayerResult> Players { get; init; } = [];
}

public sealed record MatchPlayerResult(
    string PlayerId,
    string DisplayName,
    int FinalScore,
    int DrawScore,
    int GuessScore,
    int CorrectGuesses,
    bool IsWinner
);
