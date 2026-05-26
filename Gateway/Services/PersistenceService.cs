using Gateway.Data;
using Microsoft.Data.Sqlite;
using Shared.Models;

namespace Gateway.Services;

public sealed class PersistenceService
{
    private readonly string _connectionString;
    private readonly MatchRepository _matchRepository;
    private readonly PlayerStatsRepository _playerStatsRepository;

    public PersistenceService(string connectionString)
        : this(connectionString, new MatchRepository(), new PlayerStatsRepository())
    {
    }

    public PersistenceService(
        string connectionString,
        MatchRepository matchRepository,
        PlayerStatsRepository playerStatsRepository)
    {
        _connectionString = connectionString;
        _matchRepository = matchRepository;
        _playerStatsRepository = playerStatsRepository;
    }

    public async Task SaveMatchResultAsync(MatchResult result, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(result.RoomCode) || result.FinalScores.Count == 0)
        {
            throw new InvalidOperationException("Match result is incomplete.");
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Member D: one transaction keeps Matches, MatchPlayers and PlayerStats consistent.
            var roomId = $"room-{result.RoomCode}";
            var matchId = Guid.NewGuid().ToString("N");

            await _matchRepository.SaveAsync(
                connection,
                (SqliteTransaction)transaction,
                result,
                roomId,
                matchId,
                cancellationToken);

            await _playerStatsRepository.UpsertAfterMatchAsync(
                connection,
                (SqliteTransaction)transaction,
                result,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<MatchResult>> GetMatchHistoryAsync(
        string userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await _matchRepository.GetHistoryByUserIdAsync(connection, userId, limit, cancellationToken);
    }

    public async Task<PlayerStatsResult> GetPlayerStatsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await _playerStatsRepository.GetByUserIdAsync(connection, userId, cancellationToken);
    }
}
