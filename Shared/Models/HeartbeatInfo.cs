using Shared.Enums;

namespace Shared.Models;

public sealed record HeartbeatInfo(
    string ServerId,
    int ActiveRooms,
    int ActivePlayers,
    double CpuLoad,
    double MemoryUsage,
    bool CanAcceptRoom,
    NodeStatus Status,
    DateTimeOffset LastSeen
);
