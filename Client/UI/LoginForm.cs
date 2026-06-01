using Client.Services;
using Client.State;
using Client.Utils; // THÊM DÒNG NÀY ĐỂ SỬ DỤNG APP_THEME
using Shared.Enums;
using System;
using System.Drawing;
using System.Windows.Forms;
using Siticone.Desktop.UI.WinForms;

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
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;

        // CẬP NHẬT: Sử dụng Dark theme cho Form
        AppTheme.ApplyDarkForm(this);

        // Khởi tạo Form bo góc (Đã sửa lỗi ContainerControl)
        _borderlessForm = new SiticoneBorderlessForm()
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        // Kéo thả Form bằng thanh tiêu đề giả
        var dragControl = new SiticoneDragControl { TargetControl = this };

        // Nút tắt Form ở góc trên cùng bên phải
        var exitButton = new SiticoneControlBox { Anchor = AnchorStyles.Top | AnchorStyles.Right, FillColor = Color.Transparent, IconColor = AppTheme.SubText, Left = 410, Top = 0 };

        var title = new Label
        {
            Text = "Pictionary Online",
            AutoSize = true,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Primary, // Màu tím chủ đạo của hệ thống
            Left = 30,
            Top = 20
        };

        var info = new Label
        {
            Text = "Baseline client: connect to Gateway only.",
            AutoSize = true,
            Font = AppTheme.NormalFont,
            ForeColor = AppTheme.SubText,
            Left = 30,
            Top = 60
        };

        // --- CÁC TRƯỜNG NHẬP LIỆU ---
        var ipLabel = new Label { Text = "Gateway IP:", Left = 30, Top = 103, AutoSize = true };
        AppTheme.StyleLabel(ipLabel);
        var ipInput = new SiticoneTextBox { Text = "127.0.0.1", Left = 140, Top = 95, Width = 150, Height = 36 };
        AppTheme.StyleTextBox(ipInput);

        var portLabel = new Label { Text = "Port:", Left = 30, Top = 148, AutoSize = true };
        AppTheme.StyleLabel(portLabel);
        var portInput = new SiticoneTextBox { Text = "5000", Left = 140, Top = 140, Width = 80, Height = 36 };
        AppTheme.StyleTextBox(portInput);

        var userLabel = new Label { Text = "Username:", Left = 30, Top = 193, AutoSize = true };
        AppTheme.StyleLabel(userLabel);
        var userInput = new SiticoneTextBox
        {
            Left = 140,
            Top = 185,
            Width = 200,
            Height = 36,
            PlaceholderText = "Nhập tài khoản..."
        };
        AppTheme.StyleTextBox(userInput);

        var passLabel = new Label { Text = "Password:", Left = 30, Top = 238, AutoSize = true };
        AppTheme.StyleLabel(passLabel);
        var passInput = new SiticoneTextBox
        {
            Left = 140,
            Top = 230,
            Width = 200,
            Height = 36,
            UseSystemPasswordChar = true,
            PlaceholderText = "Nhập mật khẩu..."
        };
        AppTheme.StyleTextBox(passInput);

        var statusLabel = new Label { Text = "Chưa kết nối.", Left = 30, Top = 285, AutoSize = true };
        AppTheme.StyleLabel(statusLabel);
        statusLabel.ForeColor = AppTheme.SubText;

        // --- CÁC NÚT TƯƠNG TÁC ---
        var connectButton = new SiticoneButton
        {
            Text = "Connect",
            Left = 30,
            Top = 320,
            Width = 105,
            Height = 40,
            Cursor = Cursors.Hand
        };
        AppTheme.StylePrimaryButton(connectButton);

        var loginButton = new SiticoneButton
        {
            Text = "Login",
            Left = 145,
            Top = 320,
            Width = 105,
            Height = 40,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleSuccessButton(loginButton);

        var registerButton = new SiticoneButton
        {
            Text = "Register",
            Left = 260,
            Top = 320,
            Width = 105,
            Height = 40,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        // Style thủ công theo chuẩn Warning của Theme (Do AppTheme chưa viết sẵn StyleWarningButton)
        registerButton.FillColor = AppTheme.Warning;
        registerButton.ForeColor = AppTheme.Text;
        registerButton.BorderRadius = 8;
        registerButton.Font = AppTheme.HeaderFont;

        // --- XỬ LÝ LỖI MẤT KẾT NỐI TỪ CODE GỐC CỦA NHÓM ---
        _socketService.ReceiveError += (_, error) =>
        {
            UpdateUiSafe(() =>
            {
                statusLabel.Text = error;
                statusLabel.ForeColor = AppTheme.Danger; // Đổi sang màu Danger thống nhất
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
                    statusLabel.ForeColor = AppTheme.Danger; // Đổi sang màu Danger thống nhất
                }
            });
        };

        // --- XỬ LÝ TIN NHẮN SOCKET TỪ SERVER ---
        _socketService.MessageReceived += (_, message) =>
        {
            UpdateUiSafe(() =>
            {
                _dispatcher.Dispatch(message);

                switch (message.Type)
                {
                    case MessageType.LoginSuccess when !string.IsNullOrWhiteSpace(_state.SessionId):
                        statusLabel.Text = $"Xin chào, {_state.Username ?? userInput.Text}!";
                        statusLabel.ForeColor = AppTheme.Success;

                        // 1. Ẩn LoginForm đi
                        Hide();

                        // 2. Gọi màn hình chào Logo chạy loading (Dùng ShowDialog để ép đợi chạy xong)
                        using (var splashForm = new SplashForm())
                        {
                            splashForm.ShowDialog();
                        }

                        // 3. Sau khi SplashForm đóng, tự động mở và chuyển sang LobbyForm
                        var lobbyForm = new LobbyForm(_state, _socketService);
                        lobbyForm.FormClosed += (s, args) => Show();
                        lobbyForm.Show();
                        break;

                    case MessageType.LoginSuccess:
                        statusLabel.Text = "Lỗi: Không nhận được SessionId từ Server.";
                        statusLabel.ForeColor = AppTheme.Danger;
                        break;

                    case MessageType.LoginFailed:
                    case MessageType.RegisterFailed:
                    case MessageType.Error:
                        statusLabel.Text = _state.LastErrorMessage ?? "Yêu cầu thất bại.";
                        statusLabel.ForeColor = AppTheme.Danger;

                        if (message.Type == MessageType.LoginFailed)
                            MessageBox.Show("Sai tài khoản hoặc mật khẩu! Vui lòng thử lại.", "Lỗi Đăng Nhập", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        else if (message.Type == MessageType.RegisterFailed)
                            MessageBox.Show("Tên tài khoản này đã bị trùng!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;

                    case MessageType.RegisterSuccess:
                        statusLabel.Text = "Đăng ký thành công!";
                        statusLabel.ForeColor = AppTheme.Success;
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
                statusLabel.ForeColor = AppTheme.Danger;
                return;
            }

            statusLabel.Text = "Connecting...";
            statusLabel.ForeColor = AppTheme.Primary;
            connectButton.Enabled = false;

            try
            {
                await _socketService.ConnectAsync(host, port);
                statusLabel.Text = "Connected to Gateway.";
                statusLabel.ForeColor = AppTheme.Success;
                loginButton.Enabled = true;
                registerButton.Enabled = true;
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Connection failed: {ex.Message}";
                statusLabel.ForeColor = AppTheme.Danger;
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
        AppTheme.ApplyCornerLogo(this, "TopRight");
    }

    // Hàm đảm bảo update UI an toàn (tránh lỗi cross-thread)
    private void UpdateUiSafe(Action update)
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(update);
    }
}
