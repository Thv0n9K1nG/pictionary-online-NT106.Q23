using Client.Services;
using Client.State;
using Shared.Enums;
using Shared.Models;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Client.UI;

public sealed class LoginForm : Form
{
    private readonly ClientState _state = new();
    private readonly SocketService _socketService = new();
    private readonly MessageDispatcher _dispatcher; // Khai báo Dispatcher

    public LoginForm()
    {
        _dispatcher = new MessageDispatcher(_state); // Khởi tạo Dispatcher truyền state vào

        Text = "Pictionary Online - Login";
        Width = 460;  // Mở rộng form một chút để đủ chỗ
        Height = 360; // Tăng chiều cao
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

        // --- CÁC TRƯỜNG NHẬP LIỆU GATEWAY (GIỮ NGUYÊN) ---
        var ipLabel = new Label { Text = "Gateway IP:", Left = 30, Top = 100, AutoSize = true };
        var ipInput = new TextBox { Text = "127.0.0.1", Left = 120, Top = 97, Width = 150 };

        var portLabel = new Label { Text = "Port:", Left = 30, Top = 130, AutoSize = true };
        var portInput = new TextBox { Text = "8080", Left = 120, Top = 127, Width = 80 };

        // --- CÁC TRƯỜNG NHẬP LIỆU TÀI KHOẢN (MỚI) ---
        var userLabel = new Label { Text = "Username:", Left = 30, Top = 160, AutoSize = true };
        var userInput = new TextBox { Left = 120, Top = 157, Width = 150 };

        var passLabel = new Label { Text = "Password:", Left = 30, Top = 190, AutoSize = true };
        var passInput = new TextBox { Left = 120, Top = 187, Width = 150, UseSystemPasswordChar = true };

        var statusLabel = new Label { Text = "Chưa kết nối.", Left = 30, Top = 230, AutoSize = true, ForeColor = Color.Gray };

        // --- CÁC NÚT TƯƠNG TÁC ---
        var connectButton = new Button { Text = "Connect", Left = 30, Top = 270, Width = 80 };
        var loginButton = new Button { Text = "Login", Left = 120, Top = 270, Width = 80, Enabled = false };
        var registerButton = new Button { Text = "Register", Left = 210, Top = 270, Width = 80, Enabled = false };
        var openLobbyButton = new Button { Text = "Open Lobby", Left = 300, Top = 270, Width = 100, Enabled = false };

        // --- SỰ KIỆN LẮNG NGHE SOCKET (MỚI) ---
        _socketService.MessageReceived += (sender, message) =>
        {
            // Bắt buộc gọi Invoke để tương tác với UI từ một background thread
            if (IsHandleCreated)
            {
                Invoke(() => 
                {
                    _dispatcher.Dispatch(message);
                    
                    // Nếu Dispatcher xử lý Login thành công, mở khóa nút vào sảnh
                    if (message.Type == MessageType.LoginSuccess)
                    {
                        statusLabel.Text = $"Xin chào, {_state.Username ?? userInput.Text}!";
                        statusLabel.ForeColor = Color.Green;
                        openLobbyButton.Enabled = true;
                    }
                });
            }
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
                
                // Mở khóa các nút đăng nhập / đăng ký
                loginButton.Enabled = true;
                registerButton.Enabled = true;
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Kết nối thất bại: {ex.Message}";
                statusLabel.ForeColor = Color.Red;
                connectButton.Enabled = true;
            }
        };

        // --- SỰ KIỆN ĐĂNG KÝ (MỚI) ---
        registerButton.Click += async (_, _) =>
        {
            string user = userInput.Text.Trim();
            string pass = passInput.Text.Trim();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Vui lòng nhập Username và Password!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            await _socketService.SendAsync(GameMessageFactory.Register(user, pass));
        };

        // --- SỰ KIỆN ĐĂNG NHẬP (MỚI) ---
        loginButton.Click += async (_, _) =>
        {
            string user = userInput.Text.Trim();
            string pass = passInput.Text.Trim();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Vui lòng nhập Username và Password!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _state.Username = user; // Tạm lưu Username vào State
            await _socketService.SendAsync(GameMessageFactory.Login(user, pass));
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
        Controls.Add(userLabel);
        Controls.Add(userInput);
        Controls.Add(passLabel);
        Controls.Add(passInput);
        Controls.Add(statusLabel);
        Controls.Add(connectButton);
        Controls.Add(loginButton);
        Controls.Add(registerButton);
        Controls.Add(openLobbyButton);
    }
}
