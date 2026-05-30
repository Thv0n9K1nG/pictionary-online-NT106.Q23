using Client.Services;
using Client.State;
using Shared.Enums;
using System.Drawing;
using System.Windows.Forms;

namespace Client.UI;

public sealed class LoginForm : Form
{
    private readonly ClientState _state = new();
    private readonly SocketService _socketService = new();
    private readonly MessageDispatcher _dispatcher;

    public LoginForm()
    {
        _dispatcher = new MessageDispatcher(_state);

        Text = "Pictionary Online - Login";
        Width = 460;
        Height = 360;
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
            Text = "Client connects to Gateway only.",
            AutoSize = true,
            Left = 30,
            Top = 60
        };

        var ipLabel = new Label { Text = "Gateway IP:", Left = 30, Top = 100, AutoSize = true };
        var ipInput = new TextBox { Text = "127.0.0.1", Left = 120, Top = 97, Width = 150 };

        var portLabel = new Label { Text = "Port:", Left = 30, Top = 130, AutoSize = true };
        var portInput = new TextBox { Text = "5000", Left = 120, Top = 127, Width = 80 };

        var userLabel = new Label { Text = "Username:", Left = 30, Top = 160, AutoSize = true };
        var userInput = new TextBox { Left = 120, Top = 157, Width = 150 };

        var passLabel = new Label { Text = "Password:", Left = 30, Top = 190, AutoSize = true };
        var passInput = new TextBox { Left = 120, Top = 187, Width = 150, UseSystemPasswordChar = true };

        var statusLabel = new Label { Text = "Not connected.", Left = 30, Top = 230, Width = 390, ForeColor = Color.Gray };

        var connectButton = new Button { Text = "Connect", Left = 30, Top = 270, Width = 80 };
        var loginButton = new Button { Text = "Login", Left = 120, Top = 270, Width = 80, Enabled = false };
        var registerButton = new Button { Text = "Register", Left = 210, Top = 270, Width = 80, Enabled = false };
        var openLobbyButton = new Button { Text = "Open Lobby", Left = 300, Top = 270, Width = 100, Enabled = false };

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
                    openLobbyButton.Enabled = false;
                    statusLabel.Text = "Disconnected from Gateway.";
                    statusLabel.ForeColor = Color.Red;
                }
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
                        statusLabel.Text = $"Xin chao, {_state.Username ?? userInput.Text}!";
                        statusLabel.ForeColor = Color.Green;
                        openLobbyButton.Enabled = true;
                        break;
                    case MessageType.LoginSuccess:
                        statusLabel.Text = "Login response did not include a sessionId.";
                        statusLabel.ForeColor = Color.Red;
                        openLobbyButton.Enabled = false;
                        break;
                    case MessageType.LoginFailed:
                    case MessageType.RegisterFailed:
                    case MessageType.Error:
                        statusLabel.Text = _state.LastErrorMessage ?? "Request failed.";
                        statusLabel.ForeColor = Color.Red;
                        openLobbyButton.Enabled = false;
                        break;
                    case MessageType.RegisterSuccess:
                        statusLabel.Text = "Register successful. You can login now.";
                        statusLabel.ForeColor = Color.Green;
                        break;
                }
            });
        };

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
            statusLabel.ForeColor = Color.Blue;
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
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Please enter username and password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show("Please enter username and password.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _state.Username = user;
            await _socketService.SendAsync(GameMessageFactory.Login(user, pass));
        };

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

    private void UpdateUiSafe(Action update)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        BeginInvoke(update);
    }
}
