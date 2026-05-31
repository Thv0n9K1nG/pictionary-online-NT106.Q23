using Client.Controls;
using Client.Services;
using Client.State;
using Shared.Models;
using Shared.Enums;
using System;
using System.Drawing;
using System.Threading;
using System.Linq;
using System.Windows.Forms;
using Siticone.Desktop.UI.WinForms;

namespace Client.UI;

public sealed class GameForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly MessageDispatcher _dispatcher;
    private readonly DrawingCanvas _canvas = new();

    private readonly Label _lblTimer = new();
    private readonly SiticoneProgressBar _timerBar = new();
    private readonly Label _lblHint = new();
    private readonly SiticoneButton _btnReady = new();
    private readonly RichTextBox _chatBox = new();
    private readonly SiticoneTextBox _txtGuess = new();
    private readonly SiticoneButton _btnSend = new();
    private readonly ListView _scoreboard = new();
    private SiticonePanel toolbarPanel = new SiticonePanel();

    private SiticoneBorderlessForm _borderlessForm;

    private GameState _lastGameState = GameState.Waiting;
    private readonly SemaphoreSlim _drawSendLock = new(1, 1);

    private readonly System.Windows.Forms.Timer _countdownTimer = new();
    private DateTime _roundEndTime;

    public GameForm(ClientState state, SocketService socketService, MessageDispatcher dispatcher)
    {
        _state = state;
        _socketService = socketService;
        _dispatcher = dispatcher;

        InitializeUi();
        RegisterEvents();
    }

    private void InitializeUi()
    {
        Text = "Pictionary Online - Vòng chơi";
        Width = 1160;
        Height = 830;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(240, 244, 249);

        _borderlessForm = new SiticoneBorderlessForm()
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        var dragControl = new SiticoneDragControl { TargetControl = this };
        var exitButton = new SiticoneControlBox { Anchor = AnchorStyles.Top | AnchorStyles.Right, FillColor = Color.Transparent, IconColor = Color.Gray, Left = 1110, Top = 0 };
        Controls.Add(exitButton);

        var canvasContainer = new SiticonePanel { Left = 20, Top = 40, Width = 800, Height = 600, BorderRadius = 10, FillColor = Color.White, BorderColor = Color.LightGray, BorderThickness = 1 };
        _canvas.Left = 0; _canvas.Top = 0; _canvas.Width = 800; _canvas.Height = 600;
        _canvas.BorderStyle = BorderStyle.None;
        canvasContainer.Controls.Add(_canvas);
        Controls.Add(canvasContainer);

        SetupToolbar();

        _lblHint.Text = "💡 Gợi ý: _ _ _ _";
        _lblHint.Left = 25;
        _lblHint.Top = 750;
        _lblHint.AutoSize = true;
        _lblHint.Font = new Font("Segoe UI", 18, FontStyle.Bold);
        _lblHint.ForeColor = Color.FromArgb(94, 148, 255);
        Controls.Add(_lblHint);

        int rightX = 840;
        int rightWidth = 290;

        // ==========================================
        // FIX LỖI 1: CĂN GIỮA VÀ GIÃN CÁCH ĐỒNG HỒ
        // ==========================================
        _lblTimer.Text = "⏳ 60";
        _lblTimer.Font = new Font("Segoe UI", 24, FontStyle.Bold); // Giảm size font 1 chút
        _lblTimer.AutoSize = false; // Tắt AutoSize để ép căn giữa
        _lblTimer.Width = rightWidth; // Rộng bằng cái thanh Bar
        _lblTimer.Height = 45;
        _lblTimer.TextAlign = ContentAlignment.MiddleCenter; // Nằm ở giữa cực kỳ cân đối
        _lblTimer.Left = rightX;
        _lblTimer.Top = 18; // Kéo lên cao cho thoáng
        Controls.Add(_lblTimer);

        _timerBar.Left = rightX;
        _timerBar.Top = 70; // Đẩy thanh chạy xuống dưới 1 chút để tạo khoảng hở (padding)
        _timerBar.Width = rightWidth;
        _timerBar.Height = 15;
        _timerBar.BorderRadius = 7;
        _timerBar.Maximum = 60;
        _timerBar.Value = 60;
        _timerBar.ProgressColor = Color.FromArgb(46, 204, 113);
        _timerBar.ProgressColor2 = Color.FromArgb(46, 204, 113);
        Controls.Add(_timerBar);

        _btnReady.Text = "SẴN SÀNG";
        _btnReady.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        _btnReady.BorderRadius = 8;
        _btnReady.FillColor = Color.FromArgb(46, 204, 113);
        _btnReady.ForeColor = Color.White;
        _btnReady.Left = rightX;
        _btnReady.Top = 105;
        _btnReady.Width = rightWidth;
        _btnReady.Height = 50;
        _btnReady.Cursor = Cursors.Hand;
        Controls.Add(_btnReady);

        Label lblScore = new Label { Text = "🏆 Bảng điểm", Left = rightX, Top = 175, Font = new Font("Segoe UI", 11, FontStyle.Bold), AutoSize = true };
        Controls.Add(lblScore);

        var scorePanel = new SiticonePanel { Left = rightX, Top = 205, Width = rightWidth, Height = 170, BorderRadius = 10, FillColor = Color.White, BorderColor = Color.LightGray, BorderThickness = 1 };
        _scoreboard.Left = 5; _scoreboard.Top = 5; _scoreboard.Width = rightWidth - 10; _scoreboard.Height = 160;
        _scoreboard.View = View.Details; _scoreboard.FullRowSelect = true; _scoreboard.GridLines = true;
        _scoreboard.BorderStyle = BorderStyle.None;
        _scoreboard.Font = new Font("Segoe UI", 10);
        _scoreboard.Columns.Add("Người chơi", 190); _scoreboard.Columns.Add("Điểm", 80);
        scorePanel.Controls.Add(_scoreboard);
        Controls.Add(scorePanel);

        Label lblChat = new Label { Text = "💬 Khung Chat", Left = rightX, Top = 385, Font = new Font("Segoe UI", 11, FontStyle.Bold), AutoSize = true };
        Controls.Add(lblChat);

        var chatPanel = new SiticonePanel { Left = rightX, Top = 415, Width = rightWidth, Height = 250, BorderRadius = 10, FillColor = Color.White, BorderColor = Color.LightGray, BorderThickness = 1 };
        _chatBox.Left = 5; _chatBox.Top = 5; _chatBox.Width = rightWidth - 10; _chatBox.Height = 240;
        _chatBox.ReadOnly = true; _chatBox.BackColor = Color.White; _chatBox.BorderStyle = BorderStyle.None;
        _chatBox.Font = new Font("Segoe UI", 10);
        chatPanel.Controls.Add(_chatBox);
        Controls.Add(chatPanel);

        _txtGuess.Left = rightX;
        _txtGuess.Top = 680;
        _txtGuess.Width = 205;
        _txtGuess.Height = 45;
        _txtGuess.BorderRadius = 8;
        _txtGuess.Font = new Font("Segoe UI", 11);
        _txtGuess.PlaceholderText = "Đoán chữ tại đây...";
        Controls.Add(_txtGuess);

        _btnSend.Text = "Gửi";
        _btnSend.Left = rightX + 215;
        _btnSend.Top = 680;
        _btnSend.Width = 75;
        _btnSend.Height = 45;
        _btnSend.BorderRadius = 8;
        _btnSend.FillColor = Color.FromArgb(94, 148, 255);
        _btnSend.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        _btnSend.Cursor = Cursors.Hand;
        Controls.Add(_btnSend);

        _countdownTimer.Interval = 1000;
        _countdownTimer.Tick += CountdownTimer_Tick;
    }

    private void SetupToolbar()
    {
        toolbarPanel = new SiticonePanel { Left = 20, Top = 650, Width = 800, Height = 85, FillColor = Color.White, BorderRadius = 10, BorderColor = Color.LightGray, BorderThickness = 1 };
        Controls.Add(toolbarPanel);

        int currentX = 15;
        toolbarPanel.Controls.Add(new Label { Text = "Công cụ", Left = currentX, Top = 8, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic), BackColor = Color.White });

        SiticoneButton btnPen = new SiticoneButton { Text = "✏️", Left = currentX, Top = 30, Width = 45, Height = 40, BorderRadius = 5, FillColor = Color.FromArgb(240, 240, 240), ForeColor = Color.Black, Cursor = Cursors.Hand };
        btnPen.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = false; };
        toolbarPanel.Controls.Add(btnPen); currentX += 50;

        SiticoneButton btnEraser = new SiticoneButton { Text = "🧼", Left = currentX, Top = 30, Width = 45, Height = 40, BorderRadius = 5, FillColor = Color.FromArgb(240, 240, 240), ForeColor = Color.Black, Cursor = Cursors.Hand };
        btnEraser.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = true; };
        toolbarPanel.Controls.Add(btnEraser); currentX += 50;

        SiticoneButton btnClear = new SiticoneButton { Text = "🗑️", Left = currentX, Top = 30, Width = 45, Height = 40, BorderRadius = 5, FillColor = Color.MistyRose, ForeColor = Color.Black, Cursor = Cursors.Hand };
        btnClear.Click += (s, e) => { if (MessageBox.Show("Xóa sạch bảng vẽ?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes) _canvas.ClearCanvas(); };
        toolbarPanel.Controls.Add(btnClear); currentX += 65;

        toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 12, BackColor = Color.Gainsboro }); currentX += 15;

        toolbarPanel.Controls.Add(new Label { Text = "Hình khối", Left = currentX, Top = 8, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic), BackColor = Color.White });
        string[] shapes = { "➖", "⬜", "⭕", "🔺" };
        DrawTool[] tools = { DrawTool.Line, DrawTool.Rectangle, DrawTool.Ellipse, DrawTool.Triangle };
        for (int i = 0; i < shapes.Length; i++)
        {
            var btn = new SiticoneButton { Text = shapes[i], Left = currentX, Top = 30, Width = 40, Height = 40, BorderRadius = 5, FillColor = Color.FromArgb(240, 240, 240), ForeColor = Color.Black, Cursor = Cursors.Hand };
            int index = i;
            btn.Click += (s, e) => { _canvas.CurrentTool = tools[index]; _canvas.IsEraser = false; };
            toolbarPanel.Controls.Add(btn); currentX += 45;
        }

        currentX += 10;
        toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 12, BackColor = Color.Gainsboro }); currentX += 15;

        // ==========================================
        // FIX LỖI 2: VẼ VÒNG TRÒN CỠ CỌ XỊN XÒ
        // ==========================================
        toolbarPanel.Controls.Add(new Label { Text = "Cỡ cọ", Left = currentX, Top = 8, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic), BackColor = Color.White });

        int[] actualSizes = { 2, 6, 14 };       // Size thật sự xuất ra màn hình vẽ
        int[] visualSizes = { 6, 12, 20 };      // Đường kính hình tròn hiển thị trên nút (để user dễ nhìn)

        for (int i = 0; i < actualSizes.Length; i++)
        {
            var btnSize = new SiticoneButton { Text = "", Left = currentX, Top = 30, Width = 45, Height = 40, BorderRadius = 5, FillColor = Color.FromArgb(240, 240, 240), Cursor = Cursors.Hand };

            int realSize = actualSizes[i];
            int vSize = visualSizes[i];

            // Ma thuật GDI+: Tự động vẽ một hình tròn xám đen lên chính giữa nút
            btnSize.Paint += (sender, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; // Khử răng cưa cho viền tròn mượt
                using var brush = new SolidBrush(Color.FromArgb(60, 60, 60)); // Màu cọ xám đen

                // Toán học căn giữa: (Rộng_Nút - Rộng_VòngTròn)/2
                int xPos = (btnSize.Width - vSize) / 2;
                int yPos = (btnSize.Height - vSize) / 2;
                e.Graphics.FillEllipse(brush, xPos, yPos, vSize, vSize);
            };

            btnSize.Click += (s, e) => _canvas.BrushSize = realSize;
            toolbarPanel.Controls.Add(btnSize); currentX += 50;
        }

        currentX += 10;
        toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 12, BackColor = Color.Gainsboro }); currentX += 15;

        toolbarPanel.Controls.Add(new Label { Text = "Màu sắc", Left = currentX, Top = 8, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic), BackColor = Color.White });
        string[] colors = { "#000000", "#FF0000", "#0000FF", "#008000", "#FFFF00", "#FFA500", "#FFFFFF", "#7F7F7F", "#880015", "#ED1C24" };
        int colorX = currentX; int colorY = 25;
        for (int i = 0; i < colors.Length; i++)
        {
            var btnColor = new SiticoneButton { FillColor = ColorTranslator.FromHtml(colors[i]), Left = colorX, Top = colorY, Width = 24, Height = 24, BorderRadius = 12, Cursor = Cursors.Hand };
            if (colors[i] == "#FFFFFF") btnColor.BorderThickness = 1;
            string hex = colors[i];
            btnColor.Click += (s, e) => { _canvas.CurrentColor = hex; _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = false; };
            toolbarPanel.Controls.Add(btnColor);
            colorX += 28;
            if (i == 4) { colorX = currentX; colorY += 28; }
        }
    }

    private void RegisterEvents()
    {
        _canvas.LocalDraw += OnCanvasLocalDraw;
        _state.OnDrawDataReceived += OnRemoteDrawReceived;

        _dispatcher.TimerUpdated += OnTimerUpdated;
        _dispatcher.HintReceived += UpdateHint;
        _dispatcher.GameplayStateChanged += UpdateGameplayControls;
        _dispatcher.PlayerListUpdated += UpdateScoreboard;
        _dispatcher.WordOptionsReceived += ShowWordSelection;
        _dispatcher.RoundEnded += ShowRoundResult;
        _dispatcher.GameEnded += ShowGameResult;

        _btnReady.Click += BtnReady_Click;
        _btnSend.Click += BtnSend_Click;
        _txtGuess.KeyDown += TxtGuess_KeyDown;

        _dispatcher.SystemMessageReceived += msg => AppendChat("🔔 [Hệ thống] " + msg, Color.Blue);
        _dispatcher.CorrectGuessReceived += (playerName, scoreAwarded) => {
            var scoreText = scoreAwarded > 0 ? $" (+{scoreAwarded} điểm)" : string.Empty;
            AppendChat($"✅ {playerName} đã đoán đúng!{scoreText}", Color.ForestGreen);
        };
    }

    private void UpdateGameplayControls()
    {
        if (InvokeRequired) { Invoke(UpdateGameplayControls); return; }

        if ((_lastGameState == GameState.Waiting || _lastGameState == GameState.RoundEnd || _lastGameState == GameState.GameOver) &&
            (_state.CurrentGameState == GameState.SelectingWord || _state.CurrentGameState == GameState.Drawing))
        {
            _canvas.ClearCanvas(false);
        }

        _lastGameState = _state.CurrentGameState;

        _canvas.CanDraw = _state.IsDrawer && _state.CurrentGameState == GameState.Drawing;

        _txtGuess.Enabled = !_state.IsDrawer && _state.CurrentGameState == GameState.Drawing;
        _btnSend.Enabled = _txtGuess.Enabled;

        _btnReady.Enabled = (_state.CurrentGameState is GameState.Waiting or GameState.RoundEnd) &&
                            !string.IsNullOrWhiteSpace(_state.RoomCode) && !string.IsNullOrWhiteSpace(_state.SessionId);

        toolbarPanel.Visible = _state.IsDrawer;

        if (_state.IsDrawer && _state.CurrentGameState == GameState.Drawing)
        {
            _lblHint.Text = "💡 Bạn đang vẽ! Hãy vẽ thật đẹp để mọi người cùng đoán nhé.";
            _lblHint.ForeColor = Color.ForestGreen;
        }
        else if (_state.CurrentGameState != GameState.Drawing)
        {
            _countdownTimer.Stop();
        }
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        int remaining = (int)(_roundEndTime - DateTime.Now).TotalSeconds;

        if (remaining > 0)
            UpdateTimerUI(remaining);
        else
        {
            UpdateTimerUI(0);
            _countdownTimer.Stop();
        }
    }

    private void UpdateTimerUI(int seconds)
    {
        int displaySeconds = Math.Max(0, seconds);

        // Đã áp dụng căn giữa nên không cần thụt lề bằng space nữa
        _lblTimer.Text = $"⏳ {displaySeconds}";

        if (displaySeconds <= _timerBar.Maximum)
        {
            _timerBar.Value = displaySeconds;
        }

        if (displaySeconds <= 10)
        {
            _lblTimer.ForeColor = Color.Red;
            _timerBar.ProgressColor = Color.Red;
            _timerBar.ProgressColor2 = Color.Red;
        }
        else if (displaySeconds <= 20)
        {
            _lblTimer.ForeColor = Color.DarkOrange;
            _timerBar.ProgressColor = Color.DarkOrange;
            _timerBar.ProgressColor2 = Color.DarkOrange;
        }
        else
        {
            _lblTimer.ForeColor = Color.Black;
            _timerBar.ProgressColor = Color.FromArgb(46, 204, 113);
            _timerBar.ProgressColor2 = Color.FromArgb(46, 204, 113);
        }
    }

    public void OnTimerUpdated(int remainingSeconds)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnTimerUpdated(remainingSeconds))); return; }

        if (_state.CurrentGameState != GameState.Drawing)
        {
            _state.CurrentGameState = GameState.Drawing;
            UpdateGameplayControls();
        }

        _roundEndTime = DateTime.Now.AddSeconds(remainingSeconds);

        if (remainingSeconds > 0)
        {
            _timerBar.Maximum = remainingSeconds > 60 ? remainingSeconds : 60;
        }

        UpdateTimerUI(remainingSeconds);
        _countdownTimer.Start();
    }

    private void UpdateHint(string hint)
    {
        if (InvokeRequired) { Invoke(() => UpdateHint(hint)); return; }

        if (!_state.IsDrawer)
        {
            string rawHint = hint.Replace(" ", "");
            int letterCount = rawHint.Length;
            string spacedHint = string.Join(" ", rawHint.ToCharArray());
            _lblHint.Text = $"💡 Gợi ý: {spacedHint} ({letterCount} chữ cái)";
            _lblHint.ForeColor = Color.FromArgb(94, 148, 255);
        }
    }

    private async void OnCanvasLocalDraw(object? sender, Shared.Models.DrawPayload payload)
    {
        if (!_socketService.IsConnected || string.IsNullOrEmpty(_state.RoomCode)) return;
        if (_state.CurrentGameState != GameState.Drawing) return;

        bool isAcquired = await _drawSendLock.WaitAsync(5);
        if (!isAcquired) return;

        try
        {
            if (_state.CurrentGameState != GameState.Drawing) return;
            var message = GameMessageFactory.Draw(_state.RoomCode, payload, _state.SessionId ?? "");
            await _socketService.SendAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi khi gửi nét vẽ: {ex.Message}");
        }
        finally
        {
            _drawSendLock.Release();
        }
    }

    public void OnRemoteDrawReceived(DrawPayload payload)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnRemoteDrawReceived(payload))); return; }
        _canvas.DrawFromRemote(payload);
    }

    private async void BtnReady_Click(object? sender, EventArgs e)
    {
        if (!EnsureGameplayContext()) return;
        await _socketService.SendAsync(GameMessageFactory.Ready(_state.RoomCode!, _state.SessionId!));
        _btnReady.Enabled = false;
        _btnReady.Text = "ĐANG CHỜ MỌI NGƯỜI...";
        _btnReady.FillColor = Color.Gray;
    }

    private async void BtnSend_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtGuess.Text)) return;
        AppendChat($"[{_state.Username}]: {_txtGuess.Text}", Color.Black);
        if (!EnsureGameplayContext()) return;
        await _socketService.SendAsync(GameMessageFactory.Guess(_state.RoomCode!, _txtGuess.Text.Trim(), _state.SessionId!));
        _txtGuess.Clear();
    }

    private void TxtGuess_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            BtnSend_Click(sender!, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
    }

    private void UpdateScoreboard(System.Collections.Generic.List<PlayerInfo> players)
    {
        if (InvokeRequired) { Invoke(() => UpdateScoreboard(players)); return; }
        _scoreboard.BeginUpdate();
        _scoreboard.Items.Clear();
        foreach (var p in players)
        {
            string name = p.DisplayName;
            if (p.IsHost) name = "👑 " + name;
            if (p.IsDrawer) name = "🖌️ " + name;

            var item = new ListViewItem(name);
            item.SubItems.Add(p.Score.ToString());

            if (p.IsDrawer)
            {
                item.BackColor = Color.LightGoldenrodYellow;
                item.Font = new Font(_scoreboard.Font, FontStyle.Bold);
            }
            if (!p.IsConnected) item.ForeColor = Color.Gray;
            _scoreboard.Items.Add(item);
        }
        _scoreboard.EndUpdate();
        UpdateGameplayControls();
    }

    private void AppendChat(string message, Color color)
    {
        if (InvokeRequired) { Invoke(() => AppendChat(message, color)); return; }
        _chatBox.SelectionStart = _chatBox.TextLength;
        _chatBox.SelectionLength = 0;
        _chatBox.SelectionColor = color;
        _chatBox.AppendText(message + Environment.NewLine);
        _chatBox.SelectionColor = _chatBox.ForeColor;
        _chatBox.ScrollToCaret();
    }

    private void ShowWordSelection(System.Collections.Generic.List<string> words)
    {
        if (InvokeRequired) { Invoke(() => ShowWordSelection(words)); return; }
        using var form = new WordSelectionForm(words);
        if (form.ShowDialog() == DialogResult.OK)
        {
            if (!EnsureGameplayContext()) return;
            _ = _socketService.SendAsync(GameMessageFactory.SelectWord(_state.RoomCode!, form.SelectedWord, _state.SessionId!));
        }
    }

    private void ShowRoundResult(System.Collections.Generic.List<PlayerInfo> players, bool gameEnded)
    {
        if (InvokeRequired) { Invoke(() => ShowRoundResult(players, gameEnded)); return; }
        _countdownTimer.Stop();
        UpdateScoreboard(players);

        if (!gameEnded)
        {
            using var result = new ResultForm("Kết quả Vòng", players.Select(player => (player.DisplayName, player.Score)).ToList());
            result.ShowDialog(this);
            _btnReady.Enabled = true;
            _btnReady.Text = "SẴN SÀNG";
            _btnReady.FillColor = Color.FromArgb(46, 204, 113);
        }
    }

    private void ShowGameResult(MatchResult result)
    {
        if (InvokeRequired) { Invoke(() => ShowGameResult(result)); return; }
        _countdownTimer.Stop();

        var rows = result.FinalScores
            .Select(score => {
                var name = _state.PlayerList.FirstOrDefault(player => player.PlayerId == score.Key)?.DisplayName ?? score.Key;
                return (name, score.Value);
            })
            .OrderByDescending(row => row.Value)
            .ToList();
        using var form = new ResultForm("Kết quả Chung cuộc", rows);
        form.ShowDialog(this);
        UpdateGameplayControls();
    }

    private bool EnsureGameplayContext()
    {
        if (!string.IsNullOrWhiteSpace(_state.RoomCode) && !string.IsNullOrWhiteSpace(_state.SessionId)) return true;
        AppendChat("🔔 [Lỗi] Thiếu thông tin phòng, vui lòng kết nối lại.", Color.Firebrick);
        return false;
    }
}