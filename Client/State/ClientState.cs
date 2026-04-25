using Shared.Enums;
using Shared.Models;

namespace Client.State;

public sealed class ClientState
{
    public string? SessionId { get; set; }
    public string? PlayerId { get; set; }
    public string? Username { get; set; }
    public string? RoomCode { get; set; }
    public GameState CurrentGameState { get; set; } = GameState.Waiting;
    public bool IsDrawer { get; set; }
    public int CurrentScore { get; set; }
    public List<PlayerInfo> PlayerList { get; } = [];
    public int LatestTimerValue { get; set; }
    public string? LastErrorMessage { get; set; }
}
