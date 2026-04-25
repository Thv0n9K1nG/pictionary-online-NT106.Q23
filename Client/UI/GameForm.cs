using Client.Controls;
using Client.Services;
using Client.State;

namespace Client.UI;

public sealed class GameForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly DrawingCanvas _canvas = new();

    public GameForm(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;

        Text = "Pictionary Online - Game";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        _canvas.Left = 20;
        _canvas.Top = 20;
        _canvas.CanDraw = true;

        var note = new Label
        {
            Text = "Game baseline - DRAW will be sent to Gateway in real implementation.",
            AutoSize = true,
            Left = 20,
            Top = 640
        };

        Controls.Add(_canvas);
        Controls.Add(note);
    }
}
