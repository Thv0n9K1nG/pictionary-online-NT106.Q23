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

namespace Client.UI;

public sealed class GameForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly MessageDispatcher _dispatcher;
    private readonly DrawingCanvas _canvas = new();

    // UI Components
    private readonly Label _lblTimer = new();
    private readonly ProgressBar _timerBar = new(); // F-26B: Thanh đếm ngược trực quan
    private readonly Label _lblHint = new();
    private readonly Button _btnReady = new();
    private readonly RichTextBox _chatBox = new();
    private readonly TextBox _txtGuess = new();
    private readonly Button _btnSend = new();
    private readonly ListView _scoreboard = new();
    private Panel toolbarPanel = new Panel();

    // Logic & Threading
    private GameState _lastGameState = GameState.Waiting;
    private readonly SemaphoreSlim _drawSendLock = new(1, 1);

    // F-26B: Bộ đếm ngược Localeeê
    private readonly System.Windows.Forms.Timer _countdownTimer = new();
    private DateTime _roundEndTime; // THAY ĐỔI Ở ĐÂY: Dùng thời gian tuyệt đối

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
        Width = 1150;
        Height = 810;

        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;

        // --- KHU VỰC BÊN TRÁI: BẢNG VẼ & CÔNG CỤ ---
        _canvas.Width = 800;
        _canvas.Height = 600;
        _canvas.Left = 20;
        _canvas.Top = 20;
        _canvas.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_canvas);

        SetupToolbar();

        _lblHint.Text = "💡 Gợi ý: _ _ _ _";
        _lblHint.Left = 20;
        _lblHint.Top = 720; // Đẩy chữ xuống xíu cho cách đều Toolbar

        // 2. BẬT AUTOSIZE VÀ BỎ FIXED WIDTH ĐỂ CHỮ KHÔNG BỊ CẮT XÉN
        _lblHint.AutoSize = true;

        _lblHint.Font = new Font("Consolas", 18, FontStyle.Bold);
        _lblHint.ForeColor = Color.DarkSlateBlue;
        Controls.Add(_lblHint);

        // --- KHU VỰC BÊN PHẢI: BẢNG ĐIỂM, CHAT & TƯƠNG TÁC ---
        int rightX = 835;
        int rightWidth = 275;

        _lblTimer.Text = "⏳ 60";
        _lblTimer.Font = new Font("Segoe UI", 26, FontStyle.Bold);
        _lblTimer.AutoSize = true;
        _lblTimer.Left = rightX;
        _lblTimer.Top = 15;
        Controls.Add(_lblTimer);

        // Thanh tiến trình trực quan
        _timerBar.Left = rightX;
        _timerBar.Top = 65;
        _timerBar.Width = rightWidth;
        _timerBar.Height = 12;
        _timerBar.Maximum = 60;
        _timerBar.Value = 60;
        _timerBar.Style = ProgressBarStyle.Continuous;
        Controls.Add(_timerBar);

        _btnReady.Text = "SẴN SÀNG";
        _btnReady.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        _btnReady.BackColor = Color.MediumSeaGreen;
        _btnReady.ForeColor = Color.White;
        _btnReady.FlatStyle = FlatStyle.Flat;
        _btnReady.FlatAppearance.BorderSize = 0;
        _btnReady.Left = rightX;
        _btnReady.Top = 90;
        _btnReady.Width = rightWidth;
        _btnReady.Height = 45;
        _btnReady.Cursor = Cursors.Hand;
        Controls.Add(_btnReady);

        Label lblScore = new Label { Text = "🏆 Bảng điểm", Left = rightX, Top = 145, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true };
        Controls.Add(lblScore);

        _scoreboard.Left = rightX;
        _scoreboard.Top = 170;
        _scoreboard.Width = rightWidth;
        _scoreboard.Height = 180;
        _scoreboard.View = View.Details;
        _scoreboard.FullRowSelect = true;
        _scoreboard.GridLines = true;
        _scoreboard.Font = new Font("Segoe UI", 10);
        _scoreboard.Columns.Add("Người chơi", 180);
        _scoreboard.Columns.Add("Điểm", 70);
        Controls.Add(_scoreboard);

        Label lblChat = new Label { Text = "💬 Khung Chat", Left = rightX, Top = 360, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true };
        Controls.Add(lblChat);

        _chatBox.Left = rightX;
        _chatBox.Top = 385;
        _chatBox.Width = rightWidth;
        _chatBox.Height = 265;
        _chatBox.ReadOnly = true;
        _chatBox.BackColor = Color.WhiteSmoke;
        _chatBox.BorderStyle = BorderStyle.FixedSingle;
        _chatBox.Font = new Font("Segoe UI", 9);
        Controls.Add(_chatBox);

        _txtGuess.Left = rightX;
        _txtGuess.Top = 665;
        _txtGuess.Width = 200;
        _txtGuess.Font = new Font("Segoe UI", 12);
        Controls.Add(_txtGuess);

        _btnSend.Text = "Gửi";
        _btnSend.Left = rightX + 205;
        _btnSend.Top = 664;
        _btnSend.Width = 70;
        _btnSend.Height = 28;
        _btnSend.BackColor = Color.DodgerBlue;
        _btnSend.ForeColor = Color.White;
        _btnSend.FlatStyle = FlatStyle.Flat;
        _btnSend.FlatAppearance.BorderSize = 0;
        _btnSend.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _btnSend.Cursor = Cursors.Hand;
        Controls.Add(_btnSend);

        // Khởi động sự kiện đếm ngược
        _countdownTimer.Interval = 1000;
        _countdownTimer.Tick += CountdownTimer_Tick;
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

    private void SetupToolbar()
    {
        toolbarPanel = new Panel { Left = 20, Top = 630, Width = 800, Height = 80, BackColor = Color.WhiteSmoke, BorderStyle = BorderStyle.FixedSingle };
        Controls.Add(toolbarPanel);

        int currentX = 10;
        toolbarPanel.Controls.Add(new Label { Text = "Công cụ", Left = currentX, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic) });

        Button btnPen = new Button { Text = "✏️", Left = currentX, Top = 25, Width = 40, Height = 40, FlatStyle = FlatStyle.Flat };
        btnPen.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = false; };
        toolbarPanel.Controls.Add(btnPen); currentX += 45;

        Button btnEraser = new Button { Text = "🧼", Left = currentX, Top = 25, Width = 40, Height = 40, FlatStyle = FlatStyle.Flat };
        btnEraser.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = true; };
        toolbarPanel.Controls.Add(btnEraser); currentX += 45;

        Button btnClear = new Button { Text = "🗑️", Left = currentX, Top = 25, Width = 40, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.MistyRose };
        btnClear.Click += (s, e) => { if (MessageBox.Show("Xóa sạch bảng vẽ?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes) _canvas.ClearCanvas(); };
        toolbarPanel.Controls.Add(btnClear); currentX += 55;

        toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 10, BackColor = Color.DarkGray }); currentX += 15;

        toolbarPanel.Controls.Add(new Label { Text = "Hình khối", Left = currentX, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic) });
        Button btnLine = new Button { Text = "➖", Left = currentX, Top = 25, Width = 35, Height = 40, FlatStyle = FlatStyle.Flat };
        btnLine.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Line; _canvas.IsEraser = false; };
        toolbarPanel.Controls.Add(btnLine); currentX += 40;

        Button btnRect = new Button { Text = "⬜", Left = currentX, Top = 25, Width = 35, Height = 40, FlatStyle = FlatStyle.Flat };
        btnRect.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Rectangle; _canvas.IsEraser = false; };
        toolbarPanel.Controls.Add(btnRect); currentX += 40;

        Button btnEllipse = new Button { Text = "⭕", Left = currentX, Top = 25, Width = 35, Height = 40, FlatStyle = FlatStyle.Flat };
        btnEllipse.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Ellipse; _canvas.IsEraser = false; };
        toolbarPanel.Controls.Add(btnEllipse); currentX += 40;

        Button btnTri = new Button { Text = "🔺", Left = currentX, Top = 25, Width = 35, Height = 40, FlatStyle = FlatStyle.Flat };
        btnTri.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Triangle; _canvas.IsEraser = false; };
        toolbarPanel.Controls.Add(btnTri); currentX += 50;

        toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 10, BackColor = Color.DarkGray }); currentX += 15;

        toolbarPanel.Controls.Add(new Label { Text = "Cỡ cọ", Left = currentX, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic) });
        int[] sizes = { 2, 6, 14 };
        string[] sizeLabels = { "●", "●●", "●●●" };
        for (int i = 0; i < sizes.Length; i++)
        {
            Button btnSize = new Button { Text = sizeLabels[i], Left = currentX, Top = 25, Width = 40, Height = 40, FlatStyle = FlatStyle.Flat };
            int size = sizes[i];
            btnSize.Click += (s, e) => _canvas.BrushSize = size;
            toolbarPanel.Controls.Add(btnSize);
            currentX += 45;
        }

        toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 10, BackColor = Color.DarkGray }); currentX += 15;

        toolbarPanel.Controls.Add(new Label { Text = "Màu", Left = currentX, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic) });
        string[] colors = { "#000000", "#FF0000", "#0000FF", "#008000", "#FFFF00", "#FFA500", "#FFFFFF", "#7F7F7F", "#880015", "#ED1C24" };
        int colorX = currentX;
        int colorY = 20;
        for (int i = 0; i < colors.Length; i++)
        {
            Button btnColor = new Button { BackColor = ColorTranslator.FromHtml(colors[i]), Left = colorX, Top = colorY, Width = 25, Height = 25, FlatStyle = FlatStyle.Flat };
            string hex = colors[i];
            btnColor.Click += (s, e) => { _canvas.CurrentColor = hex; _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = false; };
            toolbarPanel.Controls.Add(btnColor);
            colorX += 28;
            if (i == 4) { colorX = currentX; colorY += 28; }
        }
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

        // Keep the timer alive for both drawer and guessers while a round is active.
        if (_state.IsDrawer && _state.CurrentGameState == GameState.Drawing)
        {
            _lblHint.Text = "💡 Bạn đang vẽ! Hãy vẽ thật đẹp để mọi người cùng đoán nhé.";
            _lblHint.ForeColor = Color.ForestGreen;
        }

        else if (_state.CurrentGameState != GameState.Drawing)
        {
            // Nếu thoát trạng thái chơi thì dừng đồng hồ ngay lập tức
            _countdownTimer.Stop();
        }
        // =====================================================================
    }

    // ==========================================
    // LOGIC ĐẾM NGƯỢC TẠI MÁY CLIENT (F-26B) - ĐÃ FIX LỖI DESYNC
    // ==========================================
    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        // Tính số giây còn lại dựa trên đồng hồ thực tế của máy tính
        int remaining = (int)(_roundEndTime - DateTime.Now).TotalSeconds;

        if (remaining > 0)
        {
            UpdateTimerUI(remaining);
        }
        else
        {
            UpdateTimerUI(0);
            _countdownTimer.Stop(); // Dừng khi hết giờ
        }
    }

    private void UpdateTimerUI(int seconds)
    {
        // Math.Max để đề phòng trường hợp lag mạng làm thời gian bị âm
        int displaySeconds = Math.Max(0, seconds);
        _lblTimer.Text = $"⏳ {displaySeconds}";

        if (displaySeconds <= _timerBar.Maximum)
        {
            _timerBar.Value = displaySeconds;
        }

        // Đổi màu cảnh báo
        if (displaySeconds <= 10) _lblTimer.ForeColor = Color.Red;
        else if (displaySeconds <= 20) _lblTimer.ForeColor = Color.DarkOrange;
        else _lblTimer.ForeColor = Color.Black;
    }

    public void OnTimerUpdated(int remainingSeconds)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnTimerUpdated(remainingSeconds))); return; }

        if (_state.CurrentGameState != GameState.Drawing)
        {
            _state.CurrentGameState = GameState.Drawing;
            UpdateGameplayControls();
        }

        // CHỐT MỐC THỜI GIAN TƯƠNG LAI CẦN ĐẠT TỚI
        _roundEndTime = DateTime.Now.AddSeconds(remainingSeconds);

        if (remainingSeconds > 0)
        {
            _timerBar.Maximum = remainingSeconds > 60 ? remainingSeconds : 60;
        }

        UpdateTimerUI(remainingSeconds);
        _countdownTimer.Start();
    }
    // ==========================================

    private void UpdateHint(string hint)
    {
        if (InvokeRequired) { Invoke(() => UpdateHint(hint)); return; }

        if (!_state.IsDrawer)
        {
            // 1. Loại bỏ các khoảng trắng thừa (nếu có) để đếm đúng số lượng ký tự
            string rawHint = hint.Replace(" ", "");
            int letterCount = rawHint.Length;

            // 2. Chèn khoảng cách đều nhau để UI hiển thị đẹp mắt (vd: "_ _ A _")
            string spacedHint = string.Join(" ", rawHint.ToCharArray());

            // 3. Hiển thị rõ ràng số chữ cái lên màn hình
            _lblHint.Text = $"💡 Gợi ý: {spacedHint} ({letterCount} chữ cái)";
            _lblHint.ForeColor = Color.DarkSlateBlue;
        }
    }

    private async void OnCanvasLocalDraw(object? sender, Shared.Models.DrawPayload payload)
    {
        if (!_socketService.IsConnected || string.IsNullOrEmpty(_state.RoomCode)) return;

        await _drawSendLock.WaitAsync();
        try
        {
            var message = Client.Services.GameMessageFactory.Draw(_state.RoomCode, payload, _state.SessionId ?? "");
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
        _btnReady.BackColor = Color.Gray;
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

        // Dừng đếm ngược khi hết vòng
        _countdownTimer.Stop();
        UpdateScoreboard(players);

        if (!gameEnded)
        {
            using var result = new ResultForm("Kết quả Vòng", players.Select(player => (player.DisplayName, player.Score)).ToList());
            result.ShowDialog(this);

            _btnReady.Enabled = true;
            _btnReady.Text = "SẴN SÀNG";
            _btnReady.BackColor = Color.MediumSeaGreen;
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
