using Microsoft.Data.Sqlite;
using Shared.Models;

namespace Gateway.Data;

public sealed class MatchRepository
{
    public async Task SaveAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        MatchResult result,
        string roomId,
        string matchId,
        CancellationToken cancellationToken = default)
    {
        // Member D: store the match header and every player's final row in the same transaction.
        await UpsertRoomAsync(connection, transaction, result, roomId, cancellationToken);
        await InsertMatchAsync(connection, transaction, result, roomId, matchId, cancellationToken);

        foreach (var player in BuildPlayerRows(result))
        {
            await InsertMatchPlayerAsync(connection, transaction, matchId, player, cancellationToken);
        }
    }

    private static async Task UpsertRoomAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        MatchResult result,
        string roomId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
        INSERT INTO Rooms(roomId, roomCode, status, createdAt, startedAt, endedAt)
        VALUES ($roomId, $roomCode, 'ENDED', $createdAt, $startedAt, $endedAt)
        ON CONFLICT(roomCode) DO UPDATE SET
            status = 'ENDED',
            startedAt = COALESCE(Rooms.startedAt, excluded.startedAt),
            endedAt = excluded.endedAt;
        """;
        command.Parameters.AddWithValue("$roomId", roomId);
        command.Parameters.AddWithValue("$roomCode", result.RoomCode);
        command.Parameters.AddWithValue("$createdAt", result.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$startedAt", result.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$endedAt", result.EndedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertMatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        MatchResult result,
        string roomId,
        string matchId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
        INSERT INTO Matches(matchId, roomId, roomCode, startedAt, endedAt, winnerUserId, status)
        VALUES ($matchId, $roomId, $roomCode, $startedAt, $endedAt, $winnerUserId, 'COMPLETED');
        """;
        command.Parameters.AddWithValue("$matchId", matchId);
        command.Parameters.AddWithValue("$roomId", roomId);
        command.Parameters.AddWithValue("$roomCode", result.RoomCode);
        command.Parameters.AddWithValue("$startedAt", result.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$endedAt", result.EndedAt.ToString("O"));
        command.Parameters.AddWithValue("$winnerUserId", result.WinnerId);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertMatchPlayerAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string matchId,
        MatchPlayerResult player,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
        INSERT INTO MatchPlayers(
            matchPlayerId,
            matchId,
            userId,
            displayName,
            finalScore,
            drawScore,
            guessScore,
            correctGuesses,
            isWinner)
        VALUES (
            $matchPlayerId,
            $matchId,
            $userId,
            $displayName,
            $finalScore,
            $drawScore,
            $guessScore,
            $correctGuesses,
            $isWinner);
        """;
        command.Parameters.AddWithValue("$matchPlayerId", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$matchId", matchId);
        command.Parameters.AddWithValue("$userId", player.PlayerId);
        command.Parameters.AddWithValue("$displayName", player.DisplayName);
        command.Parameters.AddWithValue("$finalScore", player.FinalScore);
        command.Parameters.AddWithValue("$drawScore", player.DrawScore);
        command.Parameters.AddWithValue("$guessScore", player.GuessScore);
        command.Parameters.AddWithValue("$correctGuesses", player.CorrectGuesses);
        command.Parameters.AddWithValue("$isWinner", player.IsWinner ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static IReadOnlyList<MatchPlayerResult> BuildPlayerRows(MatchResult result)
    {
        if (result.Players.Count > 0)
        {
            return result.Players;
        }

        return result.FinalScores
            .Select(score => new MatchPlayerResult(
                score.Key,
                score.Key,
                score.Value,
                0,
                score.Value,
                0,
                score.Key == result.WinnerId))
            .ToList();
    }
}
