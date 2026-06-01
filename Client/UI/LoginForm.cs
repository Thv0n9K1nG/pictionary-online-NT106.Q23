using Client.Services;
using Client.State;
using Client.Utils;
using Shared.Enums;
using Siticone.Desktop.UI.WinForms;

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
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;

        AppTheme.ApplyDarkForm(this);

        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        _ = new SiticoneDragControl { TargetControl = this };

        var exitButton = new SiticoneControlBox
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FillColor = Color.Transparent,
            IconColor = AppTheme.SubText,
            Left = 410,
            Top = 0
        };

        var title = new Label
        {
            Text = "Pictionary Online",
            AutoSize = true,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Primary,
            Left = 30,
            Top = 20
        };

        var userLabel = new Label { Text = "Username:", Left = 30, Top = 193, AutoSize = true };
        AppTheme.StyleLabel(userLabel);
        var userInput = new SiticoneTextBox
        {
            Left = 140,
            Top = 185,
            Width = 200,
            Height = 36,
            PlaceholderText = "Username..."
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
            PlaceholderText = "Password..."
        };
        AppTheme.StyleTextBox(passInput);

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
        registerButton.FillColor = AppTheme.Warning;
        registerButton.ForeColor = AppTheme.Text;
        registerButton.BorderRadius = 8;
        registerButton.Font = AppTheme.HeaderFont;

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

        Shown += (_, _) =>
        {
            Hide();
            ShowConnectionForm();
            _reconnectService.StartGatewayConnectionLoop(_state);
        };
        FormClosed += (_, _) =>
        {
            CloseConnectionForm();
            _reconnectService.Dispose();
        };

        Controls.Add(exitButton);
        Controls.Add(title);
        Controls.Add(userLabel);
        Controls.Add(userInput);
        Controls.Add(passLabel);
        Controls.Add(passInput);
        Controls.Add(loginButton);
        Controls.Add(registerButton);
        AppTheme.ApplyCornerLogo(this, "TopRight");

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
