using Client.Controls;
using Client.Services;
using Client.State;
using Shared.Enums;
using Shared.Models;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Client.UI;

public sealed class GameForm : Form
{
    private readonly System.Windows.Forms.Timer _uiTimer = new();
    private int _lastTimerValue;
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

    private Panel _toolbarPanel = new Panel();
    // --- BẠN THÊM BIẾN NÀY ĐỂ NHỚ TRẠNG THÁI TRƯỚC ĐÓ ---
    private GameState _lastGameState = GameState.Waiting;

    // ĐÈN GIAO THÔNG Ở TẠI FORM: Xếp hàng nét vẽ mà không cần đụng vào SocketService
    private readonly SemaphoreSlim _drawSendLock = new(1, 1);

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
        Text = "Pictionary Online";
        Width = 1150;
        Height = 780;
        StartPosition = FormStartPosition.CenterScreen;

        _canvas.Left = 20;
        _canvas.Top = 20;
        _canvas.Width = 800;
        _canvas.Height = 600;
        Controls.Add(_canvas);

        _lblHint.Text = "Hint: _ _ _ _";
        _lblHint.Left = 20;
        _lblHint.Top = 715;
        _lblHint.Width = 500;
        _lblHint.Font = new Font("Segoe UI", 14, FontStyle.Bold);
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

        _uiTimer.Interval = 500;
        _uiTimer.Tick += (_, _) => AnimateTimer();
        _uiTimer.Start();

        SetupToolbar();
    }

    private void RegisterEvents()
    {
        _canvas.LocalDraw += OnCanvasLocalDraw;
        _state.OnDrawDataReceived += OnRemoteDrawReceived;

        _dispatcher.PlayerListUpdated += UpdateScoreboard;
        _dispatcher.TimerUpdated += UpdateTimer;
        _dispatcher.HintReceived += UpdateHint;
        _dispatcher.WordOptionsReceived += ShowWordSelection;
        _dispatcher.RoundEnded += ShowRoundResult;
        _dispatcher.GameEnded += ShowGameResult;
        _dispatcher.GameplayStateChanged += UpdateGameplayControls;

        _btnReady.Click += BtnReady_Click;
        _btnSend.Click += BtnSend_Click;
        _txtGuess.KeyDown += TxtGuess_KeyDown;

        _dispatcher.SystemMessageReceived += msg => AppendChat("[System] " + msg, Color.Blue);
        _dispatcher.CorrectGuessReceived += (playerName, scoreAwarded) => {
            var scoreText = scoreAwarded > 0 ? $" (+{scoreAwarded})" : string.Empty;
            AppendChat($"{playerName} guessed correctly{scoreText}.", Color.ForestGreen);
        };

        FormClosed += (_, _) => _uiTimer.Stop();
        UpdateGameplayControls();
    }

    private void SetupToolbar()
    {
        // GIỮ NGUYÊN BẢN 100% GIAO DIỆN HỘP BÚT ĐỒ HỌA CỦA TIẾN
        _toolbarPanel = new Panel
        {
            Left = 20,
            Top = 630,
            Width = 800,
            Height = 80,
            BackColor = Color.WhiteSmoke,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_toolbarPanel);

        int currentX = 10;
        _toolbarPanel.Controls.Add(new Label { Text = "Công cụ", Left = currentX, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic) });

        Button btnPen = new Button { Text = "✏️", Left = currentX, Top = 25, Width = 40, Height = 40, FlatStyle = FlatStyle.Flat };
        btnPen.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = false; };
        _toolbarPanel.Controls.Add(btnPen); currentX += 45;

        Button btnEraser = new Button { Text = "🧼", Left = currentX, Top = 25, Width = 40, Height = 40, FlatStyle = FlatStyle.Flat };
        btnEraser.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = true; };
        _toolbarPanel.Controls.Add(btnEraser); currentX += 45;

        Button btnClear = new Button { Text = "🗑️", Left = currentX, Top = 25, Width = 40, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.MistyRose };
        btnClear.Click += (s, e) => { if (MessageBox.Show("Xóa sạch bảng vẽ?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes) _canvas.ClearCanvas(); };
        _toolbarPanel.Controls.Add(btnClear); currentX += 55;

        _toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 10, BackColor = Color.DarkGray }); currentX += 15;

        _toolbarPanel.Controls.Add(new Label { Text = "Hình khối", Left = currentX, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic) });

        Button btnLine = new Button { Text = "➖", Left = currentX, Top = 25, Width = 35, Height = 40, FlatStyle = FlatStyle.Flat };
        btnLine.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Line; _canvas.IsEraser = false; };
        _toolbarPanel.Controls.Add(btnLine); currentX += 40;

        Button btnRect = new Button { Text = "⬜", Left = currentX, Top = 25, Width = 35, Height = 40, FlatStyle = FlatStyle.Flat };
        btnRect.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Rectangle; _canvas.IsEraser = false; };
        _toolbarPanel.Controls.Add(btnRect); currentX += 40;

        Button btnEllipse = new Button { Text = "⭕", Left = currentX, Top = 25, Width = 35, Height = 40, FlatStyle = FlatStyle.Flat };
        btnEllipse.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Ellipse; _canvas.IsEraser = false; };
        _toolbarPanel.Controls.Add(btnEllipse); currentX += 40;

        Button btnTri = new Button { Text = "🔺", Left = currentX, Top = 25, Width = 35, Height = 40, FlatStyle = FlatStyle.Flat };
        btnTri.Click += (s, e) => { _canvas.CurrentTool = DrawTool.Triangle; _canvas.IsEraser = false; };
        _toolbarPanel.Controls.Add(btnTri); currentX += 50;

        _toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 10, BackColor = Color.DarkGray }); currentX += 15;

        _toolbarPanel.Controls.Add(new Label { Text = "Cỡ cọ", Left = currentX, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic) });
        int[] sizes = { 2, 6, 14 };
        string[] sizeLabels = { "●", "●●", "●●●" };
        for (int i = 0; i < sizes.Length; i++)
        {
            Button btnSize = new Button { Text = sizeLabels[i], Left = currentX, Top = 25, Width = 40, Height = 40, FlatStyle = FlatStyle.Flat };
            int size = sizes[i];
            btnSize.Click += (s, e) => _canvas.BrushSize = size;
            _toolbarPanel.Controls.Add(btnSize);
            currentX += 45;
        }

        _toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 10, BackColor = Color.DarkGray }); currentX += 15;

        _toolbarPanel.Controls.Add(new Label { Text = "Màu", Left = currentX, Top = 5, AutoSize = true, Font = new Font("Segoe UI", 8, FontStyle.Italic) });
        string[] colors = { "#000000", "#FF0000", "#0000FF", "#008000", "#FFFF00", "#FFA500", "#FFFFFF", "#7F7F7F", "#880015", "#ED1C24" };
        int colorX = currentX;
        int colorY = 20;
        for (int i = 0; i < colors.Length; i++)
        {
            Button btnColor = new Button { BackColor = ColorTranslator.FromHtml(colors[i]), Left = colorX, Top = colorY, Width = 25, Height = 25, FlatStyle = FlatStyle.Flat };
            string hex = colors[i];
            btnColor.Click += (s, e) => { _canvas.CurrentColor = hex; _canvas.CurrentTool = DrawTool.Pen; _canvas.IsEraser = false; };
            _toolbarPanel.Controls.Add(btnColor);
            colorX += 28;
            if (i == 4) { colorX = currentX; colorY += 28; }
        }
    }

    private void UpdateTimer(int remaining)
    {
        if (InvokeRequired) { Invoke(() => UpdateTimer(remaining)); return; }

        if (_state.CurrentGameState != GameState.Drawing)
        {
            _state.CurrentGameState = GameState.Drawing;
            UpdateGameplayControls();
        }

        _lblTimer.Text = remaining.ToString();
        _lblTimer.ForeColor = remaining <= 10 ? Color.Red : remaining <= 20 ? Color.Orange : Color.Green;
        _state.LatestTimerValue = remaining;
    }

    private void UpdateGameplayControls()
    {
        if (InvokeRequired) { Invoke(UpdateGameplayControls); return; }

        //Clear bảng vẽ khi bắt đâu ván mới
        if ((_lastGameState == GameState.Waiting || _lastGameState == GameState.RoundEnd || _lastGameState == GameState.GameOver) &&
            (_state.CurrentGameState == GameState.SelectingWord || _state.CurrentGameState == GameState.Drawing))
        {
            _canvas.ClearCanvas(false);
        }

        // Lưu lại trạng thái mới để dành cho lần kiểm tra sau
        _lastGameState = _state.CurrentGameState;
        // ---------------------------------

        _canvas.CanDraw = _state.IsDrawer && _state.CurrentGameState == GameState.Drawing;
        _txtGuess.Enabled = !_state.IsDrawer && _state.CurrentGameState == GameState.Drawing;
        _btnSend.Enabled = _txtGuess.Enabled;
        _btnReady.Enabled = (_state.CurrentGameState is GameState.Waiting or GameState.RoundEnd) &&
                            !string.IsNullOrWhiteSpace(_state.RoomCode) && !string.IsNullOrWhiteSpace(_state.SessionId);
        _toolbarPanel.Visible = _state.IsDrawer;
    }

    private async void OnCanvasLocalDraw(object? sender, DrawPayload payload)
    {
        if (!_socketService.IsConnected || string.IsNullOrEmpty(_state.RoomCode)) return;

        // SỬ DỤNG KHÓA Ở ĐÂY ĐỂ TRÁNH QUÁ TẢI CHO SOCKET SERVICE CỦA ĐỒNG ĐỘI
        await _drawSendLock.WaitAsync();
        try
        {
            var message = GameMessageFactory.Draw(_state.RoomCode, payload, _state.SessionId ?? "");
            await _socketService.SendAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi gửi nét vẽ: {ex.Message}");
        }
        finally
        {
            _drawSendLock.Release();
        }
    }

    private void OnRemoteDrawReceived(DrawPayload payload)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnRemoteDrawReceived(payload));
            return;
        }
        _canvas.DrawFromRemote(payload);
    }

    private async void BtnReady_Click(object? sender, EventArgs e)
    {
        if (!EnsureGameplayContext()) return;
        await _socketService.SendAsync(GameMessageFactory.Ready(_state.RoomCode!, _state.SessionId!));
        _btnReady.Enabled = false;
    }

    private async void BtnSend_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtGuess.Text)) return;
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

    private void UpdateScoreboard(List<PlayerInfo> players)
    {
        if (InvokeRequired) { Invoke(() => UpdateScoreboard(players)); return; }
        _scoreboard.BeginUpdate();
        _scoreboard.Items.Clear();
        foreach (var p in players)
        {
            string name = p.DisplayName;
            if (p.IsHost) name = "[Host] " + name;
            if (p.IsDrawer) name = "[Draw] " + name;

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

    private void UpdateHint(string hint)
    {
        if (InvokeRequired) { Invoke(() => UpdateHint(hint)); return; }
        _lblHint.Text = $"Hint: {hint}";
    }

    private void ShowWordSelection(List<string> words)
    {
        if (InvokeRequired) { Invoke(() => ShowWordSelection(words)); return; }
        using var form = new WordSelectionForm(words);
        if (form.ShowDialog() == DialogResult.OK)
        {
            if (!EnsureGameplayContext()) return;
            _ = _socketService.SendAsync(GameMessageFactory.SelectWord(_state.RoomCode!, form.SelectedWord, _state.SessionId!));
        }
    }

    private void ShowRoundResult(List<PlayerInfo> players, bool gameEnded)
    {
        if (InvokeRequired) { Invoke(() => ShowRoundResult(players, gameEnded)); return; }
        UpdateScoreboard(players);
        if (!gameEnded)
        {
            using var result = new ResultForm("Round result", players.Select(player => (player.DisplayName, player.Score)).ToList());
            result.ShowDialog(this);
            _btnReady.Enabled = true;
        }
    }

    private void ShowGameResult(MatchResult result)
    {
        if (InvokeRequired) { Invoke(() => ShowGameResult(result)); return; }
        var rows = result.FinalScores
            .Select(score => {
                var name = _state.PlayerList.FirstOrDefault(player => player.PlayerId == score.Key)?.DisplayName ?? score.Key;
                return (name, score.Value);
            })
            .OrderByDescending(row => row.Value)
            .ToList();
        using var form = new ResultForm("Game result", rows);
        form.ShowDialog(this);
        UpdateGameplayControls();
    }

    private bool EnsureGameplayContext()
    {
        if (!string.IsNullOrWhiteSpace(_state.RoomCode) && !string.IsNullOrWhiteSpace(_state.SessionId)) return true;
        AppendChat("[System] Missing room or session. Please rejoin the room.", Color.Firebrick);
        return false;
    }

    private void AnimateTimer()
    {
        if (_lastTimerValue == _state.LatestTimerValue) return;
        _lastTimerValue = _state.LatestTimerValue;
        _lblTimer.Font = new Font(_lblTimer.Font, FontStyle.Bold);
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
}