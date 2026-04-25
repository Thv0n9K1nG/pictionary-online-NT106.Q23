namespace GameServer.Models;

public sealed class Player
{
    public required string PlayerId { get; init; }
    public required string DisplayName { get; init; }
    public int Score { get; set; }
    public bool IsConnected { get; set; } = true;
}
