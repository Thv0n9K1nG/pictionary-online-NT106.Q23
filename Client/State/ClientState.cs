using Shared.Enums;
using Shared.Models;
using System;
using System.Collections.Generic;

namespace Client.State;

public sealed class ClientState
{
    private ClientConnectionState _connectionState = ClientConnectionState.Disconnected;

    public string? SessionId { get; set; }
    public string? PlayerId { get; set; }
    public string? Username { get; set; }
    public string? RoomCode { get; set; }
    public string GatewayHost { get; set; } = "127.0.0.1";
    public int GatewayPort { get; set; } = 5000;
    public ClientConnectionState ConnectionState => _connectionState;
    public GameState CurrentGameState { get; set; } = GameState.Waiting;
    public bool IsDrawer { get; set; }
    public int CurrentScore { get; set; }
    public List<PlayerInfo> PlayerList { get; } = [];
    public List<RoomInfo> RoomList { get; } = [];
    public int LatestTimerValue { get; set; }
    public string? LastErrorMessage { get; set; }

    // Sự kiện kết nối luồng nhận gói tin mạng với UI đồ họa
    public event Action<Shared.Models.DrawPayload>? OnDrawDataReceived;
    public event Action<ClientConnectionState>? ConnectionStateChanged;

    public void SetConnectionState(ClientConnectionState state)
    {
        if (_connectionState == state)
        {
            return;
        }

        _connectionState = state;
        ConnectionStateChanged?.Invoke(state);
    }

    public void TriggerDrawDataReceived(Shared.Models.DrawPayload payload)
    {
        OnDrawDataReceived?.Invoke(payload);
    }
}
