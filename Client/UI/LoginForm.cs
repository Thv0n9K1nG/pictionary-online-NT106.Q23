using Client.Services;
using Client.State;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Client.UI;

public sealed class LoginForm : Form
{
    private readonly ClientState _state = new();
    private readonly SocketService _socketService = new();

    public LoginForm()
    {
        Text = "Pictionary Online - Login";
        Width = 420;
        Height = 320; // Chiều cao phù hợp cho các control mới
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label
        {
            Text = "Pictionary Online",
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, 16, FontStyle.Bold),
            Left = 30,
            Top = 20
        };

        var info = new Label
        {
            Text = "Baseline client: connect to Gateway only.",
            AutoSize = true,
            Left = 30,
            Top = 60
        };

        // --- CÁC TRƯỜNG NHẬP LIỆU ---
        var ipLabel = new Label { Text = "Gateway IP:", Left = 30, Top = 100, AutoSize = true };
        var ipInput = new TextBox { Text = "127.0.0.1", Left = 120, Top = 97, Width = 150 };

        var portLabel = new Label { Text = "Port:", Left = 30, Top = 130, AutoSize = true };
        var portInput = new TextBox { Text = "8080", Left = 120, Top = 127, Width = 80 };

        var statusLabel = new Label { Text = "Chưa kết nối.", Left = 30, Top = 170, AutoSize = true, ForeColor = Color.Gray };

        // --- CÁC NÚT TƯƠNG TÁC ---
        var connectButton = new Button
        {
            Text = "Connect",
            Left = 30,
            Top = 210,
            Width = 100
        };

        var openLobbyButton = new Button
        {
            Text = "Open Lobby",
            Left = 140,
            Top = 210,
            Width = 130,
            Enabled = false // Sẽ mở khóa sau khi connect thành công
        };

        // --- SỰ KIỆN KẾT NỐI ---
        connectButton.Click += async (sender, args) =>
        {
            string host = ipInput.Text.Trim();
            if (!int.TryParse(portInput.Text.Trim(), out int port))
            {
                statusLabel.Text = "Port không hợp lệ!";
                statusLabel.ForeColor = Color.Red;
                return;
            }

            statusLabel.Text = "Đang kết nối...";
            statusLabel.ForeColor = Color.Blue;
            connectButton.Enabled = false;

            try
            {
                await _socketService.ConnectAsync(host, port);
                
                statusLabel.Text = "Connected to Gateway!";
                statusLabel.ForeColor = Color.Green;
                
                openLobbyButton.Enabled = true;
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Kết nối thất bại: {ex.Message}";
                statusLabel.ForeColor = Color.Red;
                connectButton.Enabled = true;
            }
        };

        // --- SỰ KIỆN CHUYỂN TRANG ---
        openLobbyButton.Click += (_, _) =>
        {
            Hide();
            new LobbyForm(_state, _socketService).ShowDialog();
            Show();
        };

        Controls.Add(title);
        Controls.Add(info);
        Controls.Add(ipLabel);
        Controls.Add(ipInput);
        Controls.Add(portLabel);
        Controls.Add(portInput);
        Controls.Add(statusLabel);
        Controls.Add(connectButton);
        Controls.Add(openLobbyButton);
    }
}
