namespace Shared.Models;

public sealed record PlayerStatsResult(
    string UserId,
    int TotalMatches,
    int Wins,
    int TotalScore,
    int CorrectGuesses,
    int DrawScore,
    int GuessScore);
