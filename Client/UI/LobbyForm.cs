using Client.Services;
using Client.State;
using Client.Utils;
using Shared.Enums;
using Shared.Models;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using Siticone.Desktop.UI.WinForms;

namespace Client.UI;

public sealed class LobbyForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly MessageDispatcher _dispatcher;
    private bool _gameOpened;
    private bool _roomListRequestedOnShown;
    private bool _loggingOut;

    private readonly ListBox _roomList = new();
    private readonly ListBox _playerList = new();

    private readonly SiticoneTextBox _roomCodeInput = new();
    private readonly Label _statusLabel = new();

    private SiticoneBorderlessForm _borderlessForm;

    public LobbyForm(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;
        _dispatcher = new MessageDispatcher(_state);

        Text = "Pictionary Online - Lobby";
        Width = 850;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;

        AppTheme.ApplyDarkForm(this);

        _borderlessForm = new SiticoneBorderlessForm()
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        var dragControl = new SiticoneDragControl { TargetControl = this };
        var exitButton = new SiticoneControlBox { Anchor = AnchorStyles.Top | AnchorStyles.Right, FillColor = Color.Transparent, IconColor = AppTheme.Text, Left = 800, Top = 0 };

        var title = new Label
        {
            Text = $"🎨 Lobby - {_state.Username ?? _state.PlayerId ?? "Họa sĩ"}",
            AutoSize = true,
            Left = 20,
            Top = 15,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
            BackColor = Color.Transparent
        };

        // --- THANH CÔNG CỤ ---
        var createButton = new SiticoneButton { Text = "Tạo Phòng", Left = 20, Top = 65, Width = 130, Height = 40, Cursor = Cursors.Hand };
        AppTheme.StylePrimaryButton(createButton);

        // ĐÃ SỬA: Thêm UseTransparentBackground = true để xóa viền rác quanh nút bo tròn
        var refreshButton = new SiticoneButton { Text = "Làm mới", Left = 160, Top = 65, Width = 110, Height = 40, BorderRadius = 8, FillColor = AppTheme.Border, ForeColor = AppTheme.Text, Font = AppTheme.HeaderFont, Cursor = Cursors.Hand, UseTransparentBackground = true };

        var joinLabel = new Label { Text = "Mã phòng:", Left = 285, Top = 75, AutoSize = true, BackColor = Color.Transparent };
        AppTheme.StyleLabel(joinLabel);
        joinLabel.Font = AppTheme.HeaderFont;

        _roomCodeInput.Left = 400; _roomCodeInput.Top = 65; _roomCodeInput.Width = 140; _roomCodeInput.Height = 40;
        _roomCodeInput.PlaceholderText = "Nhập mã..."; _roomCodeInput.CharacterCasing = CharacterCasing.Upper;
        AppTheme.StyleTextBox(_roomCodeInput);
        _roomCodeInput.Font = AppTheme.HeaderFont;

        var joinButton = new SiticoneButton { Text = "Vào", Left = 550, Top = 65, Width = 90, Height = 40, Cursor = Cursors.Hand };
        AppTheme.StylePrimaryButton(joinButton);

        var logoutButton = new SiticoneButton { Text = "Logout", Left = 650, Top = 65, Width = 100, Height = 40, Cursor = Cursors.Hand };
        AppTheme.StyleDangerButton(logoutButton);

        var historyButton = new SiticoneButton { Text = "Lịch sử", Left = 20, Top = 450, Width = 130, Height = 45, Cursor = Cursors.Hand };
        AppTheme.StyleSecondaryButton(historyButton);

        // ĐÃ SỬA: Chữ dưới nút Tạo. Đổi sang màu AppTheme.Text (tối) và in đậm (Bold) để không bị chìm vào nền
        _statusLabel.Left = 20; _statusLabel.Top = 120; _statusLabel.Width = 800;
        _statusLabel.Text = "Chào mừng bạn đến với xưởng vẽ!";
        AppTheme.StyleLabel(_statusLabel);
        _statusLabel.Font = new Font(AppTheme.NormalFont.FontFamily, 10, FontStyle.Bold);
        _statusLabel.ForeColor = AppTheme.Text;
        _statusLabel.BackColor = Color.Transparent;

        // --- DANH SÁCH PHÒNG CHỜ ---
        var roomLabel = new Label { Text = "Phòng chờ (Nhấp đúp để vào)", Left = 20, Top = 150, AutoSize = true, BackColor = Color.Transparent };
        AppTheme.StyleLabel(roomLabel);
        roomLabel.Font = AppTheme.HeaderFont;

        var roomPanel = new SiticonePanel { Left = 20, Top = 180, Width = 390, Height = 250 };
        AppTheme.StylePanel(roomPanel);

        _roomList.Left = 10; _roomList.Top = 15; _roomList.Width = 370; _roomList.Height = 220;
        AppTheme.StyleListBox(_roomList);
        roomPanel.Controls.Add(_roomList);

        // --- DANH SÁCH NGƯỜI CHƠI ---
        var playerLabel = new Label { Text = "Họa sĩ trong phòng", Left = 430, Top = 150, AutoSize = true, BackColor = Color.Transparent };
        AppTheme.StyleLabel(playerLabel);
        playerLabel.Font = AppTheme.HeaderFont;

        var playerPanel = new SiticonePanel { Left = 430, Top = 180, Width = 390, Height = 250 };
        AppTheme.StylePanel(playerPanel);

        _playerList.Left = 10; _playerList.Top = 15; _playerList.Width = 370; _playerList.Height = 220;
        AppTheme.StyleListBox(_playerList);
        playerPanel.Controls.Add(_playerList);

        // --- NÚT VÀO GAME ---
        var openGameButton = new SiticoneButton { Text = "Bắt đầu Game", Left = 660, Top = 450, Width = 160, Height = 45, Enabled = false, Cursor = Cursors.Hand };
        AppTheme.StyleSuccessButton(openGameButton);

        // --- GÁN SỰ KIỆN ---
        createButton.Click += async (_, _) => await SendCreateRoomAsync();
        refreshButton.Click += async (_, _) => await RefreshRoomListAsync();
        joinButton.Click += async (_, _) => await SendJoinRoomAsync();
        logoutButton.Click += async (_, _) => await SendLogoutAsync();
        historyButton.Click += (_, _) => OpenHistory();
        openGameButton.Click += (_, _) => OpenGame();
        _roomList.DoubleClick += async (_, _) => { if (_roomList.SelectedItem is RoomInfo room) { _roomCodeInput.Text = room.RoomCode; await SendJoinRoomAsync(); } };
        _socketService.MessageReceived += OnMessageReceived;
        FormClosed += (_, _) => _socketService.MessageReceived -= OnMessageReceived;
        Shown += async (_, _) => await RefreshRoomListOnFirstShowAsync();

        Controls.Add(exitButton); Controls.Add(title); Controls.Add(createButton); Controls.Add(refreshButton);
        Controls.Add(joinLabel); Controls.Add(_roomCodeInput); Controls.Add(joinButton); Controls.Add(_statusLabel);
        Controls.Add(logoutButton); Controls.Add(historyButton); Controls.Add(roomLabel); Controls.Add(roomPanel); Controls.Add(playerLabel); Controls.Add(playerPanel); Controls.Add(openGameButton);

        // --- LOAD VÀ XỬ LÝ ẢNH NỀN ---
        SetupDoodleBackground();
        AppTheme.ApplyCornerLogo(this, "TopRight");
        void SetOpenGameButtonState() { openGameButton.Enabled = !string.IsNullOrWhiteSpace(_state.RoomCode); }
        async Task SendCreateRoomAsync() { if (!EnsureLoggedIn()) return; _statusLabel.Text = "Đang tạo phòng..."; await _socketService.SendAsync(GameMessageFactory.CreateRoom(GetPlayerName(), _state.SessionId!)); }
        async Task SendJoinRoomAsync() { if (!EnsureLoggedIn()) return; var roomCode = _roomCodeInput.Text.Trim().ToUpperInvariant(); if (string.IsNullOrWhiteSpace(roomCode)) { _statusLabel.Text = "Hãy nhập mã phòng trước."; _statusLabel.ForeColor = AppTheme.Danger; return; } _statusLabel.Text = $"Đang vào phòng {roomCode}..."; await _socketService.SendAsync(GameMessageFactory.JoinRoom(roomCode, GetPlayerName(), _state.SessionId!)); }
        async Task SendLogoutAsync()
        {
            if (_loggingOut) return;
            _loggingOut = true;
            logoutButton.Enabled = false;
            _statusLabel.Text = "Đang đăng xuất...";
            _statusLabel.ForeColor = AppTheme.Text;

            try
            {
                if (!string.IsNullOrWhiteSpace(_state.SessionId))
                {
                    await _socketService.SendAsync(GameMessageFactory.Logout(_state.SessionId));
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Đăng xuất thất bại: {ex.Message}";
                _statusLabel.ForeColor = AppTheme.Danger;
                _loggingOut = false;
                logoutButton.Enabled = true;
                return;
            }

            _state.ClearSession();
            Close();
        }

        async Task RefreshRoomListOnFirstShowAsync() { if (_roomListRequestedOnShown) return; _roomListRequestedOnShown = true; await RefreshRoomListAsync(); }

        async Task RefreshRoomListAsync()
        {
            if (!EnsureLoggedIn()) return;
            try { await _socketService.SendAsync(GameMessageFactory.GetRoomList()); }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Không thể tải danh sách phòng: {ex.Message}";
                _statusLabel.ForeColor = AppTheme.Danger;
            }
        }

        void OpenGame()
        {
            if (_gameOpened) return;
            if (string.IsNullOrWhiteSpace(_state.RoomCode))
            {
                _statusLabel.Text = "Hãy tạo hoặc vào phòng trước khi bắt đầu game.";
                _statusLabel.ForeColor = AppTheme.Danger;
                return;
            }

            _gameOpened = true;
            openGameButton.Enabled = false;

            try
            {
                Hide();
                using var gameForm = new GameForm(_state, _socketService, _dispatcher);
                gameForm.ShowDialog(this);
            }
            finally
            {
                _gameOpened = false;
                SetOpenGameButtonState();
                Show();
            }
        }

        bool EnsureLoggedIn() { if (!string.IsNullOrWhiteSpace(_state.SessionId)) return true; _statusLabel.Text = "Vui lòng đăng nhập trước khi dùng sảnh chờ."; _statusLabel.ForeColor = AppTheme.Danger; return false; }
        void OpenHistory()
        {
            if (!EnsureLoggedIn()) return;

            using var historyForm = new HistoryForm(_state, _socketService, _dispatcher);
            historyForm.ShowDialog(this);
        }

        string GetPlayerName() { return _state.Username ?? _state.PlayerId ?? "Player"; }

        void OnMessageReceived(object? sender, GameMessage message)
        {
            if (!IsHandleCreated) return;

            // ĐÃ SỬA: Thêm new Action() để tránh lỗi đỏ CS1660 của Visual Studio
            BeginInvoke(new Action(() =>
            {
                _dispatcher.Dispatch(message);
                switch (message.Type)
                {
                    case MessageType.RoomJoined:
                        _statusLabel.Text = $"Đã vào phòng {_state.RoomCode}.";
                        _statusLabel.ForeColor = AppTheme.Text; // Đổi thành màu tối cho dễ đọc
                        SetOpenGameButtonState();
                        break;
                    case MessageType.PlayerList: RenderPlayerList(); break;
                    case MessageType.RoomList: RenderRoomList(); break;
                    case MessageType.Error:
                        _statusLabel.Text = _state.LastErrorMessage ?? "Thao tác thất bại.";
                        _statusLabel.ForeColor = AppTheme.Danger;
                        break;
                }
            }));
        }
    }

    private void RenderRoomList() { _roomList.Items.Clear(); foreach (var room in _state.RoomList) _roomList.Items.Add(room); _roomList.DisplayMember = nameof(RoomInfo.RoomCode); }
    private void RenderPlayerList() { _playerList.Items.Clear(); foreach (var player in _state.PlayerList) _playerList.Items.Add($"🎨 {player.DisplayName} (Điểm: {player.Score}){(player.IsReady ? " - Sẵn sàng" : string.Empty)}"); }

    private void SetupDoodleBackground()
    {
        try
        {
            var bgPath = AppTheme.TryGetAssetPath("doodle_bg.png", "doodle_bg.jpg");
            if (string.IsNullOrWhiteSpace(bgPath)) return;

            Image original = Image.FromFile(bgPath);

            Bitmap bmp = new Bitmap(original.Width, original.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                // Tự động dùng màu nền hệ thống cho Doodle để đồng bộ
                g.Clear(AppTheme.DarkBg);

                ColorMatrix matrix = new ColorMatrix { Matrix33 = 0.08f }; // Mờ 8% để hài hòa
                ImageAttributes attributes = new ImageAttributes();
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                g.DrawImage(original, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
            }

            this.BackgroundImage = bmp;
            this.BackgroundImageLayout = ImageLayout.Tile;
        }
        catch { }
    }
}
