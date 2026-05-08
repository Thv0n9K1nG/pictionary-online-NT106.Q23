using System;
using System.Drawing;
using System.Windows.Forms;
using Client.Controls;
using Client.Services;
using Client.State;

namespace Client.UI;

public sealed class GameForm : Form
{
    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly DrawingCanvas _canvas = new();
    private readonly Label _lblTimer = new();

    public GameForm(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;

        Text = "Pictionary Online - Game";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        _canvas.Width = 800;
        _canvas.Height = 600;
        _canvas.Left = 20;
        _canvas.Top = 20;
        _canvas.CanDraw = true;

        _lblTimer.Text = "60";
        _lblTimer.Font = new Font("Arial", 24, FontStyle.Bold);
        _lblTimer.AutoSize = true;
        _lblTimer.Left = 850;
        _lblTimer.Top = 20;

        Controls.Add(_canvas);
        Controls.Add(_lblTimer);
        SetupToolbar();
    }

    private void SetupToolbar()
    {
        Panel toolbarPanel = new Panel
        {
            Left = 20,
            Top = 630,
            Width = 800,
            Height = 80,
            BackColor = Color.WhiteSmoke,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(toolbarPanel);

        int currentX = 10;

        // 1. NHÓM CÔNG CỤ
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

        // Vạch phân cách
        toolbarPanel.Controls.Add(new Label { Width = 2, Height = 60, Left = currentX, Top = 10, BackColor = Color.DarkGray }); currentX += 15;

        // 2. NHÓM HÌNH KHỐI
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

        // 3. KÍCH THƯỚC (3 MỨC CHUẨN)
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

        // 4. BẢNG MÀU
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

    public void OnTimerUpdated(int remainingSeconds)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnTimerUpdated(remainingSeconds))); return; }
        _lblTimer.Text = remainingSeconds.ToString();
        _lblTimer.ForeColor = remainingSeconds <= 10 ? Color.Red : Color.Black;
    }
}