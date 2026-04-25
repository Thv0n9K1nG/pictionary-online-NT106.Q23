using Client.Services;
using Client.State;

namespace Client.UI;

public sealed class LobbyForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;

    public LobbyForm(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;

        Text = "Pictionary Online - Lobby";
        Width = 600;
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;

        var label = new Label
        {
            Text = "Lobby baseline - create/join room through Gateway.",
            AutoSize = true,
            Left = 20,
            Top = 20
        };

        var openGameButton = new Button
        {
            Text = "Open Game",
            Left = 20,
            Top = 60,
            Width = 120
        };

        openGameButton.Click += (_, _) =>
        {
            Hide();
            new GameForm(_state, _socketService).ShowDialog();
            Show();
        };

        Controls.Add(label);
        Controls.Add(openGameButton);
    }
}
