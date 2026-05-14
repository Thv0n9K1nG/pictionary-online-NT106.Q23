using Client.Services;
using Client.State;
using Shared.Enums;
using Shared.Models;

namespace Client.UI;

public sealed class LobbyForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly MessageDispatcher _dispatcher;
    private readonly ListBox _roomList = new();
    private readonly ListBox _playerList = new();
    private readonly TextBox _roomCodeInput = new();
    private readonly Label _statusLabel = new();

    public LobbyForm(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;
        _dispatcher = new MessageDispatcher(_state);

        Text = "Pictionary Online - Lobby";
        Width = 760;
        Height = 460;
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label
        {
            Text = $"Lobby - {_state.Username ?? _state.PlayerId ?? "Player"}",
            AutoSize = true,
            Left = 20,
            Top = 18,
            Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold)
        };

        var createButton = new Button { Text = "Create Room", Left = 20, Top = 58, Width = 120 };
        var refreshButton = new Button { Text = "Refresh", Left = 150, Top = 58, Width = 90 };

        var joinLabel = new Label { Text = "Room code:", Left = 260, Top = 63, AutoSize = true };
        _roomCodeInput.Left = 335;
        _roomCodeInput.Top = 58;
        _roomCodeInput.Width = 100;
        _roomCodeInput.CharacterCasing = CharacterCasing.Upper;

        var joinButton = new Button { Text = "Join", Left = 445, Top = 58, Width = 80 };

        _statusLabel.Left = 20;
        _statusLabel.Top = 100;
        _statusLabel.Width = 700;
        _statusLabel.Text = "Ready.";
        _statusLabel.ForeColor = Color.DimGray;

        var roomLabel = new Label { Text = "Waiting rooms", Left = 20, Top = 132, AutoSize = true };
        _roomList.Left = 20;
        _roomList.Top = 158;
        _roomList.Width = 330;
        _roomList.Height = 210;

        var playerLabel = new Label { Text = "Players in current room", Left = 380, Top = 132, AutoSize = true };
        _playerList.Left = 380;
        _playerList.Top = 158;
        _playerList.Width = 330;
        _playerList.Height = 210;

        var openGameButton = new Button { Text = "Open Game", Left = 590, Top = 382, Width = 120, Enabled = false };

        createButton.Click += async (_, _) => await SendCreateRoomAsync();
        refreshButton.Click += async (_, _) => await _socketService.SendAsync(GameMessageFactory.GetRoomList());
        joinButton.Click += async (_, _) => await SendJoinRoomAsync();
        openGameButton.Click += (_, _) => OpenGame();

        _roomList.DoubleClick += async (_, _) =>
        {
            if (_roomList.SelectedItem is RoomInfo room)
            {
                _roomCodeInput.Text = room.RoomCode;
                await SendJoinRoomAsync();
            }
        };

        _socketService.MessageReceived += OnMessageReceived;
        FormClosed += (_, _) => _socketService.MessageReceived -= OnMessageReceived;

        Controls.Add(title);
        Controls.Add(createButton);
        Controls.Add(refreshButton);
        Controls.Add(joinLabel);
        Controls.Add(_roomCodeInput);
        Controls.Add(joinButton);
        Controls.Add(_statusLabel);
        Controls.Add(roomLabel);
        Controls.Add(_roomList);
        Controls.Add(playerLabel);
        Controls.Add(_playerList);
        Controls.Add(openGameButton);

        void SetOpenGameButtonState()
        {
            openGameButton.Enabled = !string.IsNullOrWhiteSpace(_state.RoomCode);
        }

        async Task SendCreateRoomAsync()
        {
            if (!EnsureLoggedIn())
            {
                return;
            }

            _statusLabel.Text = "Creating room...";
            await _socketService.SendAsync(GameMessageFactory.CreateRoom(GetPlayerName(), _state.SessionId!));
        }

        async Task SendJoinRoomAsync()
        {
            if (!EnsureLoggedIn())
            {
                return;
            }

            var roomCode = _roomCodeInput.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                _statusLabel.Text = "Enter a room code first.";
                _statusLabel.ForeColor = Color.Firebrick;
                return;
            }

            _statusLabel.Text = $"Joining {roomCode}...";
            await _socketService.SendAsync(GameMessageFactory.JoinRoom(roomCode, GetPlayerName(), _state.SessionId!));
        }

        void OpenGame()
        {
            Hide();
            new GameForm(_state, _socketService, _dispatcher).ShowDialog();
            Show();
        }

        bool EnsureLoggedIn()
        {
            if (!string.IsNullOrWhiteSpace(_state.SessionId))
            {
                return true;
            }

            _statusLabel.Text = "Please login before using the lobby.";
            _statusLabel.ForeColor = Color.Firebrick;
            return false;
        }

        string GetPlayerName()
        {
            return _state.Username ?? _state.PlayerId ?? "Player";
        }

        void OnMessageReceived(object? sender, GameMessage message)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            BeginInvoke(() =>
            {
                _dispatcher.Dispatch(message);
                switch (message.Type)
                {
                    case MessageType.RoomJoined:
                        _statusLabel.Text = $"Joined room {_state.RoomCode}.";
                        _statusLabel.ForeColor = Color.Green;
                        SetOpenGameButtonState();
                        break;
                    case MessageType.PlayerList:
                        RenderPlayerList();
                        break;
                    case MessageType.RoomList:
                        RenderRoomList();
                        break;
                    case MessageType.Error:
                        _statusLabel.Text = _state.LastErrorMessage ?? "Operation failed.";
                        _statusLabel.ForeColor = Color.Firebrick;
                        break;
                }
            });
        }
    }

    private void RenderRoomList()
    {
        _roomList.Items.Clear();
        foreach (var room in _state.RoomList)
        {
            _roomList.Items.Add(room);
        }

        _roomList.DisplayMember = nameof(RoomInfo.RoomCode);
    }

    private void RenderPlayerList()
    {
        _playerList.Items.Clear();
        foreach (var player in _state.PlayerList)
        {
            _playerList.Items.Add($"{player.DisplayName} - {player.Score}");
        }
    }
}
