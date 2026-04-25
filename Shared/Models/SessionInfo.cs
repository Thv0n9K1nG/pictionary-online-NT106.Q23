namespace Shared.Models;

public sealed record SessionInfo(
    string SessionId,
    string UserId,
    string PlayerId,
    string? RoomCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt
);
