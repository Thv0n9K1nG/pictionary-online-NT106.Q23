using Client.Services;
using Client.State;

namespace Client.UI;

public sealed class LoginForm : Form
{
    private readonly ClientState _state = new();
    private readonly SocketService _socketService = new();

    public LoginForm()
    {
        Text = "Pictionary Online - Login";
        Width = 420;
        Height = 260;
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

        var openLobbyButton = new Button
        {
            Text = "Open Lobby",
            Left = 30,
            Top = 100,
            Width = 140
        };

        openLobbyButton.Click += (_, _) =>
        {
            Hide();
            new LobbyForm(_state, _socketService).ShowDialog();
            Show();
        };

        Controls.Add(title);
        Controls.Add(info);
        Controls.Add(openLobbyButton);
    }
}
