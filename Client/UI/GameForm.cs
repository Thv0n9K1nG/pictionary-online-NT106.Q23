using Client.Controls;
using Client.Services;
using Client.State;
using Shared.Enums;
using Shared.Models;

namespace Client.UI;

public sealed class GameForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly MessageDispatcher _dispatcher;

    private readonly DrawingCanvas _canvas = new();

    private readonly Label _lblHint = new();
    private readonly Label _lblTimer = new();

    private readonly Button _btnReady = new();

    private readonly RichTextBox _chatBox = new();

    private readonly TextBox _txtGuess = new();

    private readonly Button _btnSend = new();

    private readonly ListView _scoreboard = new();

    public GameForm(
        ClientState state,
        SocketService socketService,
        MessageDispatcher dispatcher)
    {
        _state = state;
        _socketService = socketService;
        _dispatcher = dispatcher;

        InitializeUi();
        RegisterEvents();
    }

    private void InitializeUi()
    {
        Text = "Pictionary Online";
        Width = 1150;
        Height = 760;

        StartPosition = FormStartPosition.CenterScreen;

        _canvas.Left = 20;
        _canvas.Top = 20;
        _canvas.Width = 800;
        _canvas.Height = 600;

        Controls.Add(_canvas);

        _lblHint.Text = "Hint: _ _ _ _";
        _lblHint.Left = 20;
        _lblHint.Top = 630;
        _lblHint.Width = 500;
        _lblHint.Font = new Font("Segoe UI", 16, FontStyle.Bold);

        Controls.Add(_lblHint);

        _lblTimer.Text = "60";
        _lblTimer.Left = 850;
        _lblTimer.Top = 20;
        _lblTimer.Width = 200;
        _lblTimer.Font = new Font("Segoe UI", 24, FontStyle.Bold);

        Controls.Add(_lblTimer);

        _scoreboard.Left = 850;
        _scoreboard.Top = 80;
        _scoreboard.Width = 240;
        _scoreboard.Height = 200;

        _scoreboard.View = View.Details;
        _scoreboard.FullRowSelect = true;
        _scoreboard.GridLines = true;

        _scoreboard.Columns.Add("Player", 150);
        _scoreboard.Columns.Add("Score", 70);

        Controls.Add(_scoreboard);

        _chatBox.Left = 850;
        _chatBox.Top = 300;
        _chatBox.Width = 240;
        _chatBox.Height = 250;
        _chatBox.ReadOnly = true;

        Controls.Add(_chatBox);

        _txtGuess.Left = 850;
        _txtGuess.Top = 570;
        _txtGuess.Width = 170;

        Controls.Add(_txtGuess);

        _btnSend.Text = "Send";
        _btnSend.Left = 1030;
        _btnSend.Top = 568;
        _btnSend.Width = 60;

        Controls.Add(_btnSend);

        _btnReady.Text = "READY";
        _btnReady.Left = 850;
        _btnReady.Top = 620;
        _btnReady.Width = 240;
        _btnReady.Height = 40;

        Controls.Add(_btnReady);
    }

    private void RegisterEvents()
    {
        _dispatcher.PlayerListUpdated += UpdateScoreboard;
        _dispatcher.TimerUpdated += UpdateTimer;
        _dispatcher.HintReceived += UpdateHint;
        _dispatcher.WordOptionsReceived += ShowWordSelection;

        _btnReady.Click += BtnReady_Click;
        _btnSend.Click += BtnSend_Click;

        _txtGuess.KeyDown += TxtGuess_KeyDown;
    }

    private async void BtnReady_Click(
        object? sender,
        EventArgs e)
    {
        await _socketService.SendAsync(new GameMessage
        {
            Type = MessageType.Ready
        });

        _btnReady.Enabled = false;
    }

    private async void BtnSend_Click(
        object? sender,
        EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtGuess.Text))
            return;

        await _socketService.SendAsync(new GameMessage
        {
            Type = MessageType.Guess,
            Payload = new
            {
                message = _txtGuess.Text
            }
        });

        _txtGuess.Clear();
    }

    private void TxtGuess_KeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            BtnSend_Click(sender!, EventArgs.Empty);

            e.SuppressKeyPress = true;
        }
    }

    private void UpdateScoreboard(
        List<PlayerInfo> players)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateScoreboard(players));
            return;
        }

        _scoreboard.Items.Clear();

        foreach (var player in players)
        {
            var displayName = player.DisplayName;

            if (player.IsHost)
            {
                displayName += " 👑";
            }

            if (player.IsDrawer)
            {
                displayName += " ✏️";
            }

            var item = new ListViewItem(displayName);

            item.SubItems.Add(player.Score.ToString());

            if (!player.IsConnected)
            {
                item.ForeColor = Color.Gray;
            }

            _scoreboard.Items.Add(item);
        }
    }

    private void UpdateTimer(int remaining)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateTimer(remaining));
            return;
        }

        _lblTimer.Text = remaining.ToString();

        _lblTimer.ForeColor =
            remaining <= 10
            ? Color.Red
            : Color.Black;
    }

    private void UpdateHint(string hint)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateHint(hint));
            return;
        }

        _lblHint.Text = $"Hint: {hint}";
    }

    private void ShowWordSelection(
        List<string> words)
    {
        if (InvokeRequired)
        {
            Invoke(() => ShowWordSelection(words));
            return;
        }

        var form = new WordSelectionForm(words);

        if (form.ShowDialog() == DialogResult.OK)
        {
            _ = _socketService.SendAsync(new GameMessage
            {
                Type = MessageType.SelectWord,
                Payload = new
                {
                    word = form.SelectedWord
                }
            });
        }
    }
}
