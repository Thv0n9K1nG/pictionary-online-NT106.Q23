using Microsoft.Data.Sqlite;

namespace Gateway.Data;

public readonly record struct UserRecord(string UserId, string Username, string PasswordHash);

public sealed class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
        CREATE TABLE IF NOT EXISTS Users (
            userId TEXT PRIMARY KEY,
            username TEXT NOT NULL UNIQUE,
            passwordHash TEXT NOT NULL,
            createdAt TEXT NOT NULL,
            lastLoginAt TEXT
        );

        CREATE TABLE IF NOT EXISTS Sessions (
            sessionId TEXT PRIMARY KEY,
            userId TEXT NOT NULL,
            createdAt TEXT NOT NULL,
            expiresAt TEXT,
            revokedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS PlayerStats (
            userId TEXT PRIMARY KEY,
            totalMatches INTEGER NOT NULL DEFAULT 0,
            totalWins INTEGER NOT NULL DEFAULT 0,
            totalScore INTEGER NOT NULL DEFAULT 0,
            totalCorrectGuesses INTEGER NOT NULL DEFAULT 0,
            totalDrawScore INTEGER NOT NULL DEFAULT 0,
            totalGuessScore INTEGER NOT NULL DEFAULT 0,
            updatedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS Rooms (
            roomId TEXT PRIMARY KEY,
            roomCode TEXT NOT NULL UNIQUE,
            status TEXT NOT NULL,
            ownerServerId TEXT,
            hostUserId TEXT,
            createdAt TEXT NOT NULL,
            startedAt TEXT,
            endedAt TEXT
        );

        CREATE TABLE IF NOT EXISTS Matches (
            matchId TEXT PRIMARY KEY,
            roomId TEXT NOT NULL,
            roomCode TEXT NOT NULL,
            startedAt TEXT,
            endedAt TEXT,
            winnerUserId TEXT,
            status TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS MatchPlayers (
            matchPlayerId TEXT PRIMARY KEY,
            matchId TEXT NOT NULL,
            userId TEXT NOT NULL,
            displayName TEXT NOT NULL,
            finalScore INTEGER NOT NULL DEFAULT 0,
            drawScore INTEGER NOT NULL DEFAULT 0,
            guessScore INTEGER NOT NULL DEFAULT 0,
            correctGuesses INTEGER NOT NULL DEFAULT 0,
            isWinner INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS RoomSnapshots (
            snapshotId TEXT PRIMARY KEY,
            roomId TEXT NOT NULL,
            roomCode TEXT NOT NULL,
            ownerServerId TEXT NOT NULL,
            version INTEGER NOT NULL,
            snapshotJson TEXT NOT NULL,
            createdAt TEXT NOT NULL
        );
        """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CreateUserAsync(string userId, string username, string passwordHash, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var createUser = connection.CreateCommand();
        createUser.Transaction = (SqliteTransaction)transaction;
        createUser.CommandText = """
        INSERT INTO Users(userId, username, passwordHash, createdAt)
        VALUES ($userId, $username, $passwordHash, $createdAt);
        """;
        createUser.Parameters.AddWithValue("$userId", userId);
        createUser.Parameters.AddWithValue("$username", username);
        createUser.Parameters.AddWithValue("$passwordHash", passwordHash);
        createUser.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToString("O"));

        await createUser.ExecuteNonQueryAsync(cancellationToken);

        var createStats = connection.CreateCommand();
        createStats.Transaction = (SqliteTransaction)transaction;
        createStats.CommandText = """
        INSERT INTO PlayerStats(userId, updatedAt)
        VALUES ($userId, $updatedAt);
        """;
        createStats.Parameters.AddWithValue("$userId", userId);
        createStats.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));

        await createStats.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateLastLoginAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET lastLoginAt = $lastLoginAt WHERE userId = $userId";
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$lastLoginAt", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveSessionAsync(string sessionId, string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO Sessions (sessionId, userId, createdAt) 
                            VALUES ($sid, $uid, $cat)";
        command.Parameters.AddWithValue("$sid", sessionId);
        command.Parameters.AddWithValue("$uid", userId);
        command.Parameters.AddWithValue("$cat", DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<UserRecord?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
        SELECT userId, username, passwordHash
        FROM Users
        WHERE username = $username
        LIMIT 1;
        """;
        command.Parameters.AddWithValue("$username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UserRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2)
        );
    }
}
