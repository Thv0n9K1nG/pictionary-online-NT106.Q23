using Client.Services;
using Client.State;
using Client.Utils;
using Shared.Enums;
using Siticone.Desktop.UI.WinForms;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client.UI;

public sealed class LoginForm : Form
{
    private readonly ClientState _state = new();
    private readonly SocketService _socketService = new();
    private readonly MessageDispatcher _dispatcher;
    private readonly ReconnectService _reconnectService;
    private readonly SiticoneBorderlessForm _borderlessForm;
    private GatewayConnectionForm? _connectionForm;
    private bool _lobbyOpened;

    public LoginForm()
    {
        _dispatcher = new MessageDispatcher(_state);
        _reconnectService = new ReconnectService(_socketService);

        Text = "Pictionary Online - Login";
        Width = 460;
        Height = 560;
        StartPosition = FormStartPosition.CenterScreen;

        AppTheme.ApplyDarkForm(this);
        AppTheme.ApplyDoodleBackground(this);

        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        _ = new SiticoneDragControl { TargetControl = this };

        // ĐÃ SỬA: Thêm BackColor và UseCompatibleTextRendering
        var logoBox = new PictureBox
        {
            Left = 35,
            Top = 40,
            Width = 400,
            Height = 235,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        try
        {
            var logoPath = AppTheme.TryGetAssetPath("logo.png");
            if (!string.IsNullOrWhiteSpace(logoPath))
            {
                logoBox.Image = Image.FromFile(logoPath);
            }
        }
        catch
        {
            // Logo is optional; keep the login form usable if the asset is missing or locked.
        }

        var userLabel = new Label { Text = "Username:", Left = 30, Top = 323, AutoSize = true };
        AppTheme.StyleLabel(userLabel);
        var userInput = new SiticoneTextBox
        {
            Left = 140,
            Top = 315,
            Width = 200,
            Height = 36,
            PlaceholderText = "Username..."
        };
        AppTheme.StyleTextBox(userInput);

        var passLabel = new Label { Text = "Password:", Left = 30, Top = 368, AutoSize = true };
        AppTheme.StyleLabel(passLabel);
        var passInput = new SiticoneTextBox
        {
            Left = 140,
            Top = 360,
            Width = 200,
            Height = 36,
            UseSystemPasswordChar = true,
            PlaceholderText = "Password..."
        };
        AppTheme.StyleTextBox(passInput);

        var loginButton = new SiticoneButton
        {
            Text = "Log in",
            Left = 60,
            Top = 450,
            Width = 120,
            Height = 40,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleSuccessButton(loginButton);

        var registerButton = new SiticoneButton
        {
            Text = "Register",
            Left = 185,
            Top = 450,
            Width = 120,
            Height = 40,
            Enabled = false,
            Cursor = Cursors.Hand
        };
        // ĐÃ SỬA: Ép chữ màu Trắng và xóa viền rác 4 góc bo
        registerButton.FillColor = AppTheme.Warning;
        registerButton.ForeColor = Color.White;
        registerButton.HoverState.ForeColor = Color.White;
        registerButton.BorderRadius = 8;
        registerButton.Font = AppTheme.HeaderFont;
        registerButton.UseTransparentBackground = true;

        var exitGameButton = new SiticoneButton
        {
            Text = "Exit",
            Left = 310,
            Top = 450,
            Width = 115,
            Height = 40,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleDangerButton(exitGameButton);

        _state.ConnectionStateChanged += connectionState =>
        {
            UpdateUiSafe(() =>
            {
                var connected = connectionState == ClientConnectionState.Connected && _socketService.IsConnected;
                loginButton.Enabled = connected;
                registerButton.Enabled = connected;

                if (connected)
                {
                    CloseConnectionForm();
                    Show();
                }
                else if (!_lobbyOpened)
                {
                    Hide();
                    ShowConnectionForm();

                    if (connectionState == ClientConnectionState.Disconnected)
                    {
                        _connectionForm?.SetStatus("Disconnected from Gateway. Reconnecting...", AppTheme.Warning);
                    }
                }
            });
        };

        _reconnectService.ConnectionStatusChanged += status =>
        {
            UpdateUiSafe(() =>
            {
                if (_state.ConnectionState == ClientConnectionState.Connected)
                {
                    CloseConnectionForm();
                    Show();
                    return;
                }

                ShowConnectionForm();
                _connectionForm?.SetStatus(status, AppTheme.Primary);
            });
        };

        _reconnectService.ConnectionRetryScheduled += (delay, _) =>
        {
            UpdateUiSafe(() =>
            {
                ShowConnectionForm();
                _connectionForm?.SetStatus($"Gateway unavailable, retrying in {(int)delay.TotalSeconds}s...", AppTheme.Warning);
                loginButton.Enabled = false;
                registerButton.Enabled = false;
            });
        };

        _socketService.ReceiveError += (_, error) =>
        {
            UpdateUiSafe(() =>
            {
                ShowConnectionForm();
                _connectionForm?.SetStatus(error, AppTheme.Danger);
            });
        };

        _socketService.MessageReceived += (_, message) =>
        {
            UpdateUiSafe(() =>
            {
                _dispatcher.Dispatch(message);

                switch (message.Type)
                {
                    case MessageType.LoginSuccess when !string.IsNullOrWhiteSpace(_state.SessionId):
                        OpenLobbyOnce();
                        break;

                    case MessageType.LoginSuccess:
                        MessageBox.Show("Login response did not include a sessionId.", "Login failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;

                    case MessageType.LoginFailed:
                        MessageBox.Show("Sai tai khoan hoac mat khau. Vui long thu lai.", "Login failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;

                    case MessageType.RegisterFailed:
                        MessageBox.Show("Ten tai khoan nay da bi trung.", "Register failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;

                    case MessageType.Error:
                        MessageBox.Show(_state.LastErrorMessage ?? "Request failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;

                    case MessageType.RegisterSuccess:
                        MessageBox.Show("Tao tai khoan thanh cong. Ban co the nhan Login.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                }
            });
        };

        registerButton.Click += async (_, _) =>
        {
            var user = userInput.Text.Trim();
            var pass = passInput.Text.Trim();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Vui long nhap Username va Password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await _socketService.SendAsync(GameMessageFactory.Register(user, pass));
        };

        loginButton.Click += async (_, _) =>
        {
            var user = userInput.Text.Trim();
            var pass = passInput.Text.Trim();
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Vui long nhap Username va Password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _state.Username = user;
            await _socketService.SendAsync(GameMessageFactory.Login(user, pass));
        };

        exitGameButton.Click += (_, _) =>
        {
            using var confirm = new ExitGameConfirmForm();
            if (confirm.ShowDialog(this) == DialogResult.OK)
            {
                Application.Exit();
            }
        };

        Shown += (_, _) =>
        {
            Hide();
            ShowConnectionForm();
            _reconnectService.StartGatewayConnectionLoop(_state);
        };
        FormClosed += (_, _) =>
        {
            logoBox.Image?.Dispose();
            CloseConnectionForm();
            _reconnectService.Dispose();
        };

        Controls.Add(logoBox);
        Controls.Add(userLabel);
        Controls.Add(userInput);
        Controls.Add(passLabel);
        Controls.Add(passInput);
        Controls.Add(loginButton);
        Controls.Add(registerButton);
        Controls.Add(exitGameButton);

        void OpenLobbyOnce()
        {
            if (_lobbyOpened)
            {
                return;
            }

            _lobbyOpened = true;
            CloseConnectionForm();
            Hide();

            using (var splashForm = new SplashForm())
            {
                splashForm.ShowDialog();
            }

            var lobbyForm = new LobbyForm(_state, _socketService);
            lobbyForm.FormClosed += (_, _) =>
            {
                _lobbyOpened = false;
                Show();
            };
            lobbyForm.Show();
        }
    }

    private void ShowConnectionForm()
    {
        if (_connectionForm is not null && !_connectionForm.IsDisposed)
        {
            if (!_connectionForm.Visible)
            {
                _connectionForm.Show();
            }

            return;
        }

        _connectionForm = new GatewayConnectionForm();
        _connectionForm.FormClosed += (_, _) =>
        {
            if (_state.ConnectionState != ClientConnectionState.Connected && !_lobbyOpened)
            {
                Close();
            }
        };
        _connectionForm.Show();
    }

    private void CloseConnectionForm()
    {
        if (_connectionForm is null || _connectionForm.IsDisposed)
        {
            _connectionForm = null;
            return;
        }

        var form = _connectionForm;
        _connectionForm = null;
        form.Close();
    }

    private void UpdateUiSafe(Action update)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        BeginInvoke(update);
    }
}
