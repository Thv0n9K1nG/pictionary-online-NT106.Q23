namespace Client.UI;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Services;
using Client.State;
using Client.Utils; // THÊM DÒNG NÀY ĐỂ SỬ DỤNG APP_THEME
using Shared.Enums;
using Shared.Models;
using Siticone.Desktop.UI.WinForms; // THÊM DÒNG NÀY ĐỂ ĐỒNG BỘ GIAO DIỆN SITICONE

public sealed class HistoryForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly ListBox _historyList = new();
    private SiticoneBorderlessForm _borderlessForm;

    public HistoryForm(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;

        Text = "Pictionary Online - Match History";
        Width = 700;
        Height = 450; // Nới rộng chiều cao một chút để trừ hao khoảng trống thanh tiêu đề mới
        StartPosition = FormStartPosition.CenterScreen;

        // CẬP NHẬT: Khởi tạo Dark Theme chuẩn hóa hệ thống cho Form
        AppTheme.ApplyDarkForm(this);

        // Đồng bộ thiết kế Borderless phẳng và bo góc 15px như các Form trước
        _borderlessForm = new SiticoneBorderlessForm()
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        var dragControl = new SiticoneDragControl { TargetControl = this };
        var exitButton = new SiticoneControlBox { Anchor = AnchorStyles.Top | AnchorStyles.Right, FillColor = Color.Transparent, IconColor = AppTheme.SubText, Left = 650, Top = 0 };
        Controls.Add(exitButton);

        // Tiêu đề chữ nổi bật trên nền tối
        var titleLabel = new Label
        {
            Text = "📋 Lịch sử trận đấu",
            AutoSize = true,
            Left = 20,
            Top = 15,
            Font = AppTheme.HeaderFont,
            ForeColor = AppTheme.Text,
            BackColor = Color.Transparent
        };
        Controls.Add(titleLabel);

        // CẬP NHẬT: Bọc ListBox vào Panel có viền mịn chống thô kịch
        var historyPanel = new SiticonePanel { Left = 20, Top = 60, Width = 660, Height = 360 };
        AppTheme.StylePanel(historyPanel);

        _historyList.Left = 10;
        _historyList.Top = 10;
        _historyList.Width = 640;
        _historyList.Height = 340;
        _historyList.BorderStyle = BorderStyle.None;

        // Áp dụng style màu nền tối, màu chữ sáng cho danh sách lịch sử
        AppTheme.StyleListBox(_historyList);

        historyPanel.Controls.Add(_historyList);
        Controls.Add(historyPanel);

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
            _historyList.Items.Clear();
            _historyList.Items.Add("Error loading match history.");
        }
    }
}
