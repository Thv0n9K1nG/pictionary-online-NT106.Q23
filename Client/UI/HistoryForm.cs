namespace Client.UI;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Services;
using Client.State;
using Shared.Enums;
using Shared.Models;

public sealed class HistoryForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly ListBox _historyList;

    public HistoryForm(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;

        Text = "Pictionary Online - Match History";
        Width = 700;
        Height = 400;
        StartPosition = FormStartPosition.CenterScreen;

        // Khởi tạo ListBox thay vì DataGridView để giữ tính nhất quán với UI Project
        _historyList = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 10)
        };
        Controls.Add(_historyList);

        // Đăng ký sự kiện lắng nghe mạng
        _socketService.MessageReceived += OnMessageReceived;
        FormClosed += (_, _) => _socketService.MessageReceived -= OnMessageReceived;

        // Tự động Gửi GET_MATCH_HISTORY khi form được mở lên
        Load += async (_, _) => await RequestMatchHistoryAsync();
    }

    private async Task RequestMatchHistoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_state.SessionId)) return;
        
        var message = new GameMessage
        {
            Type = MessageType.GetMatchHistory,
            Payload = new { sessionId = _state.SessionId }
        };
        await _socketService.SendAsync(message);
    }

    private void OnMessageReceived(object? sender, GameMessage message)
    {
        if (!IsHandleCreated) return;

        BeginInvoke(() =>
        {
            if (message.Type == MessageType.MatchHistoryResult) 
            {
                RenderHistory(message);
            }
        });
    }

    private void RenderHistory(GameMessage message)
    {
        try
        {
            // Chuyển Payload thành JSON string một cách an toàn để Deserialize
            var json = JsonSerializer.Serialize(message.Payload);
            var historyList = JsonSerializer.Deserialize<List<MatchResult>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            _historyList.Items.Clear();
            if (historyList != null)
            {
                foreach (var match in historyList)
                {
                    _historyList.Items.Add($"[{match.StartedAt:dd/MM/yyyy HH:mm}] Room: {match.RoomCode} - Winner: {match.WinnerId}");
                }
            }
        }
        catch
        {
            _historyList.Items.Add("Error loading match history.");
        }
    }
}
