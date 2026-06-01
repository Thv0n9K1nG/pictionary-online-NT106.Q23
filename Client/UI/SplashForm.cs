using System;
using System.Drawing;
using System.Windows.Forms;
using Siticone.Desktop.UI.WinForms;
using Client.Utils;

namespace Client.UI;

public sealed class SplashForm : Form
{
    private readonly System.Windows.Forms.Timer _loadingTimer = new();
    private readonly SiticoneProgressBar _progressBar = new();
    private int _progressValue = 0;

    public SplashForm()
    {
        Text = "Pictionary Online - Loading";
        Width = 550;
        Height = 380;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None; // Xóa viền cổ điển

        AppTheme.ApplyDarkForm(this);

        // Bo góc Form cho đồng bộ với LoginForm
        var borderless = new SiticoneBorderlessForm { ContainerControl = this, BorderRadius = 15 };

        // --- PICTUREBOX HIỂN THỊ LOGO GAME ---
        var picLogo = new PictureBox
        {
            Width = 400,
            Height = 250,
            Left = (Width - 400) / 2, // Căn giữa theo chiều ngang
            Top = 40,                 // Cách cạnh trên 40px
            SizeMode = PictureBoxSizeMode.Zoom, // Tự động co dãn ảnh giữ nguyên tỉ lệ
            BackColor = Color.Transparent
        };

        // --- TỰ ĐỘNG QUÉT ĐƯỜNG DẪN ẢNH THÔNG MINH ---
        try
        {
            // WinForms mặc định sẽ tự tìm ở thư mục chạy exe nếu truyền tên file trực tiếp
            if (System.IO.File.Exists("logo.png"))
            {
                picLogo.Image = Image.FromFile("logo.png");
            }
            else
            {
                // Dự phòng nếu lỡ tay xóa mất file ảnh ngoài đời thực
                var lblFallback = new Label { Text = "🎨 PICTIONARY\nONLINE", Font = AppTheme.TitleFont, ForeColor = AppTheme.Primary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
                Controls.Add(lblFallback);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi đọc logo: {ex.Message}");
        }

        Controls.Add(picLogo);

        // --- THANH LOADING PROGRESS BAR ---
        _progressBar.Height = 6;
        _progressBar.Dock = DockStyle.Bottom;
        _progressBar.FillColor = AppTheme.Border;
        _progressBar.ProgressColor = AppTheme.Primary;
        _progressBar.ProgressColor2 = AppTheme.Success;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        Controls.Add(_progressBar);

        // --- TIMER ĐẾM GIỜ CHẠY ---
        _loadingTimer.Interval = 20;
        _loadingTimer.Tick += LoadingTimer_Tick;
        _loadingTimer.Start();
    }

    private void LoadingTimer_Tick(object? sender, EventArgs e)
    {
        _progressValue += 2;
        if (_progressValue >= 100)
        {
            _progressBar.Value = 100;
            _loadingTimer.Stop();
            DialogResult = DialogResult.OK;
            Close(); // Tự tắt để kích hoạt vào Lobby
        }
        else
        {
            _progressBar.Value = _progressValue;
        }
    }
}
