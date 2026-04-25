using Shared.Enums;

namespace Shared.Models;

public sealed record RoomInfo(
    string RoomCode,
    string HostName,
    int PlayerCount,
    int MaxPlayers,
    RoomStatus Status,
    string? OwnerServerId
);
