using Microsoft.Data.Sqlite;
using Shared.Models;

namespace Gateway.Data;

public sealed class PlayerStatsRepository
{
    public async Task<PlayerStatsResult> GetByUserIdAsync(
        SqliteConnection connection,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
        SELECT totalMatches, totalWins, totalScore, totalCorrectGuesses, totalDrawScore, totalGuessScore
        FROM PlayerStats
        WHERE userId = $userId
        LIMIT 1;
        """;
        command.Parameters.AddWithValue("$userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new PlayerStatsResult(userId, 0, 0, 0, 0, 0, 0);
        }

        return new PlayerStatsResult(
            userId,
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5));
    }

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
