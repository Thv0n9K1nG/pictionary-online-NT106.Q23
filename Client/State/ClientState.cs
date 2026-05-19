using Shared.Enums;
using Shared.Models;
using System;
using System.Collections.Generic;

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
    public List<RoomInfo> RoomList { get; } = [];
    public int LatestTimerValue { get; set; }
    public string? LastErrorMessage { get; set; }

    // Sự kiện kết nối luồng nhận gói tin mạng với UI đồ họa
    public event Action<Shared.Models.DrawPayload>? OnDrawDataReceived;

    public void TriggerDrawDataReceived(Shared.Models.DrawPayload payload)
    {
        OnDrawDataReceived?.Invoke(payload);
    }
}