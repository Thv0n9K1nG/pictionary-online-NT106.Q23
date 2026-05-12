namespace Shared.Models;

public sealed record PlayerInfo(
    string PlayerId,
    string DisplayName,
    int Score,
    bool IsHost,
    bool IsConnected,
    bool IsDrawer
);
