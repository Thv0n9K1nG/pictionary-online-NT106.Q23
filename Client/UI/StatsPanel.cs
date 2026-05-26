namespace Client.UI;

using System;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Services;
using Client.State;
using Shared.Enums;
using Shared.Models;

public sealed class StatsPanel : UserControl
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    
    private readonly Label _lblTotalMatches = new();
    private readonly Label _lblWins = new();
    private readonly Label _lblTotalScore = new();

    public StatsPanel(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;

        Width = 300;
        Height = 200;

        var title = new Label { Text = "Player Statistics", Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold), AutoSize = true, Left = 15, Top = 15 };
        
        _lblTotalMatches.Left = 15; _lblTotalMatches.Top = 50; _lblTotalMatches.AutoSize = true; _lblTotalMatches.Text = "Total Matches: Loading...";
        _lblWins.Left = 15; _lblWins.Top = 80; _lblWins.AutoSize = true; _lblWins.Text = "Total Wins: Loading...";
        _lblTotalScore.Left = 15; _lblTotalScore.Top = 110; _lblTotalScore.AutoSize = true; _lblTotalScore.Text = "Total Score: Loading...";

        Controls.Add(title);
        Controls.Add(_lblTotalMatches);
        Controls.Add(_lblWins);
        Controls.Add(_lblTotalScore);

        _socketService.MessageReceived += OnMessageReceived;
        
        // Tránh rò rỉ bộ nhớ (Memory leak) khi UserControl bị hủy
        HandleDestroyed += (_, _) => _socketService.MessageReceived -= OnMessageReceived;

        // Gửi GET_PLAYER_STATS
        Load += async (_, _) => await RequestPlayerStatsAsync();
    }

    private async Task RequestPlayerStatsAsync()
    {
        if (string.IsNullOrWhiteSpace(_state.SessionId)) return;
        var message = new GameMessage
        {
            Type = MessageType.GetPlayerStats,
            Payload = new { sessionId = _state.SessionId }
        };
        await _socketService.SendAsync(message);
    }

    private void OnMessageReceived(object? sender, GameMessage message)
    {
        if (!IsHandleCreated) return;

        BeginInvoke(() =>
        {
            if (message.Type == MessageType.PlayerStatsResult)
            {
                RenderStats(message);
            }
        });
    }

    private void RenderStats(GameMessage message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message.Payload);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (TryGetPropertyIgnoreCase(root, "totalMatches", out var totalMatches))
                _lblTotalMatches.Text = $"Total Matches: {totalMatches.GetInt32()}";
                
            if (TryGetPropertyIgnoreCase(root, "wins", out var wins))
                _lblWins.Text = $"Total Wins: {wins.GetInt32()}";

            if (TryGetPropertyIgnoreCase(root, "totalScore", out var score))
                _lblTotalScore.Text = $"Total Score: {score.GetInt32()}";
        }
        catch
        {
            _lblTotalMatches.Text = "Error loading stats.";
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
