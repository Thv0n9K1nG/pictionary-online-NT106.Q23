using Microsoft.Data.Sqlite;
using Shared.Models;

namespace Gateway.Data;

public sealed class PlayerStatsRepository
{
    public async Task UpsertAfterMatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        MatchResult result,
        CancellationToken cancellationToken = default)
    {
        // Member D: update aggregate stats once per completed match, outside realtime gameplay.
        foreach (var player in MatchRepository.BuildPlayerRows(result))
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
            INSERT INTO PlayerStats(
                userId,
                totalMatches,
                totalWins,
                totalScore,
                totalCorrectGuesses,
                totalDrawScore,
                totalGuessScore,
                updatedAt)
            VALUES (
                $userId,
                1,
                $wins,
                $score,
                $correctGuesses,
                $drawScore,
                $guessScore,
                $updatedAt)
            ON CONFLICT(userId) DO UPDATE SET
                totalMatches = PlayerStats.totalMatches + 1,
                totalWins = PlayerStats.totalWins + excluded.totalWins,
                totalScore = PlayerStats.totalScore + excluded.totalScore,
                totalCorrectGuesses = PlayerStats.totalCorrectGuesses + excluded.totalCorrectGuesses,
                totalDrawScore = PlayerStats.totalDrawScore + excluded.totalDrawScore,
                totalGuessScore = PlayerStats.totalGuessScore + excluded.totalGuessScore,
                updatedAt = excluded.updatedAt;
            """;
            command.Parameters.AddWithValue("$userId", player.PlayerId);
            command.Parameters.AddWithValue("$wins", player.IsWinner ? 1 : 0);
            command.Parameters.AddWithValue("$score", player.FinalScore);
            command.Parameters.AddWithValue("$correctGuesses", player.CorrectGuesses);
            command.Parameters.AddWithValue("$drawScore", player.DrawScore);
            command.Parameters.AddWithValue("$guessScore", player.GuessScore);
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
