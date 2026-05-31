using Client.Services;
using Client.State;
using Shared.Enums;
using System;
using System.Drawing;
using System.Windows.Forms;
using Siticone.Desktop.UI.WinForms; // BẮT BUỘC CÓ DÒNG NÀY

namespace Client.UI;

public sealed class LoginForm : Form
{
    private readonly ClientState _state = new();
    private readonly SocketService _socketService = new();
    private readonly MessageDispatcher _dispatcher;

    // Control tạo viền bo góc và làm mất thanh tiêu đề mặc định của WinForms
    private readonly SiticoneBorderlessForm _borderlessForm;

    public LoginForm()
    {
        _dispatcher = new MessageDispatcher(_state);

        Text = "Pictionary Online - Login";
        Width = 460;
        Height = 420; // Đã tăng chiều cao để đủ chỗ cho các TextBox cách xa nhau
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White; // Nền màu trắng tinh tế

        // Khởi tạo Form bo góc (Đã sửa lỗi ContainerControl)
        _borderlessForm = new SiticoneBorderlessForm()
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        // Kéo thả Form bằng thanh tiêu đề giả
        var dragControl = new SiticoneDragControl { TargetControl = this };

        // Nút tắt Form ở góc trên cùng bên phải
        var exitButton = new SiticoneControlBox { Anchor = AnchorStyles.Top | AnchorStyles.Right, FillColor = Color.Transparent, IconColor = Color.Gray, Left = 410, Top = 0 };

        var title = new Label
        {
            Text = "Pictionary Online",
            AutoSize = true,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(94, 148, 255), // Xanh dương Siticone
            Left = 30,
            Top = 20
        };

        var info = new Label
        {
            Text = "Baseline client: connect to Gateway only.",
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            Left = 30,
            Top = 60
        };

        // --- CÁC TRƯỜNG NHẬP LIỆU (Đã căn chỉnh lại Left = 140 chống đè chữ) ---
        var ipLabel = new Label { Text = "Gateway IP:", Left = 30, Top = 103, AutoSize = true, Font = new Font("Segoe UI", 10) };
        var ipInput = new SiticoneTextBox { Text = "127.0.0.1", Left = 140, Top = 95, Width = 150, Height = 36, BorderRadius = 5, Font = new Font("Segoe UI", 10) };

        var portLabel = new Label { Text = "Port:", Left = 30, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 10) };
        var portInput = new SiticoneTextBox { Text = "5000", Left = 140, Top = 140, Width = 80, Height = 36, BorderRadius = 5, Font = new Font("Segoe UI", 10) };

        var userLabel = new Label { Text = "Username:", Left = 30, Top = 193, AutoSize = true, Font = new Font("Segoe UI", 10) };
        var userInput = new SiticoneTextBox
        {
            Left = 140,
            Top = 185,
            Width = 200,
            Height = 36,
            BorderRadius = 5,
            PlaceholderText = "Nhập tài khoản...",
            Font = new Font("Segoe UI", 10)
        };

        var passLabel = new Label { Text = "Password:", Left = 30, Top = 238, AutoSize = true, Font = new Font("Segoe UI", 10) };
        var passInput = new SiticoneTextBox
        {
            Left = 140,
            Top = 230,
            Width = 200,
            Height = 36,
            UseSystemPasswordChar = true,
            BorderRadius = 5,
            PlaceholderText = "Nhập mật khẩu...",
            Font = new Font("Segoe UI", 10)
        };

        var statusLabel = new Label { Text = "Chưa kết nối.", Left = 30, Top = 285, AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 10) };

        // --- CÁC NÚT TƯƠNG TÁC (Width = 105, Căn cách đều) ---
        var connectButton = new SiticoneButton
        {
            Text = "Connect",
            Left = 30,
            Top = 320,
            Width = 105,
            Height = 40,
            BorderRadius = 5,
            FillColor = Color.FromArgb(94, 148, 255),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        var loginButton = new SiticoneButton
        {
            Text = "Login",
            Left = 145,
            Top = 320,
            Width = 105,
            Height = 40,
            Enabled = false,
            BorderRadius = 5,
            FillColor = Color.FromArgb(46, 204, 113),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        var registerButton = new SiticoneButton
        {
            Text = "Register",
            Left = 260,
            Top = 320,
            Width = 105,
            Height = 40,
            Enabled = false,
            BorderRadius = 5,
            FillColor = Color.FromArgb(243, 156, 18),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        // --- XỬ LÝ LỖI MẤT KẾT NỐI TỪ CODE GỐC CỦA NHÓM ---
        _socketService.ReceiveError += (_, error) =>
        {
            UpdateUiSafe(() =>
            {
                statusLabel.Text = error;
                statusLabel.ForeColor = Color.Red;
            });
        };

        _socketService.ConnectionClosed += (_, _) =>
        {
            UpdateUiSafe(() =>
            {
                if (string.IsNullOrWhiteSpace(_state.SessionId))
                {
                    connectButton.Enabled = true;
                    loginButton.Enabled = false;
                    registerButton.Enabled = false;
                    statusLabel.Text = "Disconnected from Gateway.";
                    statusLabel.ForeColor = Color.Red;
                }
            });
        };

        // --- XỬ LÝ TIN NHẮN SOCKET TỪ SERVER (Có báo lỗi & Tự động chuyển Form) ---
        _socketService.MessageReceived += (_, message) =>
        {
            UpdateUiSafe(() =>
            {
                _dispatcher.Dispatch(message);

                switch (message.Type)
                {
                    case MessageType.LoginSuccess when !string.IsNullOrWhiteSpace(_state.SessionId):
                        statusLabel.Text = $"Xin chào, {_state.Username ?? userInput.Text}!";
                        statusLabel.ForeColor = Color.Green;

                        // Chuyển sang LobbyForm tự động
                        Hide();
                        var lobbyForm = new LobbyForm(_state, _socketService);
                        lobbyForm.FormClosed += (s, args) => Show();
                        lobbyForm.Show();
                        break;

                    case MessageType.LoginSuccess:
                        statusLabel.Text = "Lỗi: Không nhận được SessionId từ Server.";
                        statusLabel.ForeColor = Color.Red;
                        break;

                    case MessageType.LoginFailed:
                    case MessageType.RegisterFailed:
                    case MessageType.Error:
                        statusLabel.Text = _state.LastErrorMessage ?? "Yêu cầu thất bại.";
                        statusLabel.ForeColor = Color.Red;

                        if (message.Type == MessageType.LoginFailed)
                            MessageBox.Show("Sai tài khoản hoặc mật khẩu! Vui lòng thử lại.", "Lỗi Đăng Nhập", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else if (message.Type == MessageType.RegisterFailed)
                            MessageBox.Show("Tên tài khoản này đã bị trùng!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;

                    case MessageType.RegisterSuccess:
                        statusLabel.Text = "Đăng ký thành công!";
                        statusLabel.ForeColor = Color.Green;
                        MessageBox.Show("Tạo tài khoản thành công! Bây giờ bạn có thể nhấn Login.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                }
            });
        };

        // --- CÁC SỰ KIỆN CLICK NÚT BẤM ---
        connectButton.Click += async (_, _) =>
        {
            var host = ipInput.Text.Trim();
            if (!int.TryParse(portInput.Text.Trim(), out var port))
            {
                statusLabel.Text = "Invalid port.";
                statusLabel.ForeColor = Color.Red;
                return;
            }

            statusLabel.Text = "Connecting...";
            statusLabel.ForeColor = Color.FromArgb(94, 148, 255);
            connectButton.Enabled = false;

            try
            {
                await _socketService.ConnectAsync(host, port);
                statusLabel.Text = "Connected to Gateway.";
                statusLabel.ForeColor = Color.Green;
                loginButton.Enabled = true;
                registerButton.Enabled = true;
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Connection failed: {ex.Message}";
                statusLabel.ForeColor = Color.Red;
                connectButton.Enabled = true;
            }
        };

        registerButton.Click += async (_, _) =>
        {
            var user = userInput.Text.Trim();
            var pass = passInput.Text.Trim();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) { MessageBox.Show("Vui lòng nhập Username và Password!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            await _socketService.SendAsync(GameMessageFactory.Register(user, pass));
        };

        loginButton.Click += async (_, _) =>
        {
            var user = userInput.Text.Trim();
            var pass = passInput.Text.Trim();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) { MessageBox.Show("Vui lòng nhập Username và Password!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            _state.Username = user;
            await _socketService.SendAsync(GameMessageFactory.Login(user, pass));
        };

        // Add control vào Form
        Controls.Add(exitButton);
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
    }

    // Hàm đảm bảo update UI an toàn (tránh lỗi cross-thread)
    private void UpdateUiSafe(Action update)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(update);
    }
}