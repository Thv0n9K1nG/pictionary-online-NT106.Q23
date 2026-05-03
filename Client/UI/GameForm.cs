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

    // Thêm Label hiển thị Timer
    private readonly Label _lblTimer = new();

    public GameForm(ClientState state, SocketService socketService)
    {
        _state = state;
        _socketService = socketService;

        Text = "Pictionary Online - Game";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        // Setup Canvas (800x600 theo yêu cầu F-15)
        _canvas.Width = 800;
        _canvas.Height = 600;
        _canvas.Left = 20;
        _canvas.Top = 20;
        _canvas.CanDraw = true;

        // Setup Timer Label
        _lblTimer.Text = "60"; // Mặc định 60 giây
        _lblTimer.Font = new Font("Arial", 24, FontStyle.Bold);
        _lblTimer.AutoSize = true;
        _lblTimer.Left = 850; // Đặt bên phải canvas
        _lblTimer.Top = 20;

        var note = new Label
        {
            Text = "Game baseline - DRAW will be sent to Gateway in real implementation.",
            AutoSize = true,
            Left = 20,
            Top = 640
        };

        Controls.Add(_canvas);
        Controls.Add(_lblTimer); // Thêm bộ đếm giờ vào Form
        Controls.Add(note);
    }

    /// <summary>
    /// Hàm này được MessageDispatcher gọi khi nhận được TIMER_UPDATE từ Gateway
    /// </summary>
    public void OnTimerUpdated(int remainingSeconds)
    {
        // Kiểm tra xem có đang ở thread khác (network thread) hay không.
        // WinForms yêu cầu mọi cập nhật UI phải chạy trên UI thread.
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnTimerUpdated(remainingSeconds)));
            return;
        }

        // Cập nhật giao diện
        _lblTimer.Text = remainingSeconds.ToString();

        // F-26: Chuyển màu đỏ khi <= 10 giây
        if (remainingSeconds <= 10)
        {
            _lblTimer.ForeColor = Color.Red;
        }
        else
        {
            _lblTimer.ForeColor = Color.Black;
        }
    }
}