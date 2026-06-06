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
            Width = 450, // ĐÃ SỬA: Tăng chiều rộng Logo lên để nhìn to rõ hơn
            Height = 280, // ĐÃ SỬA: Tăng chiều cao tương ứng
            Left = (Width - 450) / 2, // Căn giữa theo chiều ngang
            Top = 30,                 // Đẩy lên trên một chút cho cân đối
            SizeMode = PictureBoxSizeMode.Zoom, // Tự động co dãn ảnh giữ nguyên tỉ lệ
            BackColor = Color.Transparent // Để trong suốt lộ nền Doodle
        };

        // --- TỰ ĐỘNG QUÉT ĐƯỜNG DẪN ẢNH THÔNG MINH ---
        try
        {
            // WinForms mặc định sẽ tự tìm ở thư mục chạy exe nếu truyền tên file trực tiếp
            var logoPath = AppTheme.TryGetAssetPath("logo.png");
            if (!string.IsNullOrWhiteSpace(logoPath))
            {
                picLogo.Image = Image.FromFile(logoPath);
            }
            else
            {
                // Dự phòng nếu lỡ tay xóa mất file ảnh ngoài đời thực
                var lblFallback = new Label { Text = "🎨 PICTIONARY\nONLINE", Font = AppTheme.TitleFont, ForeColor = AppTheme.Primary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
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

        // --- GỌI HÀM VẼ DOODLE CHO SPLASH FORM ---
        SetupDoodleBackground();

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

    // HÀM TỰ ĐỘNG TẠO NỀN DOODLE ĐỒNG BỘ THEO MÀU THEME HỆ THỐNG
    private void SetupDoodleBackground()
    {
        try
        {
            var bgPath = AppTheme.TryGetAssetPath("doodle_bg.png");
            if (!string.IsNullOrWhiteSpace(bgPath))
            {
                using var img = Image.FromFile(bgPath);
                var bmp = new Bitmap(img.Width, img.Height);
                using var g = Graphics.FromImage(bmp);

                g.Clear(AppTheme.DarkBg);

                var colorMatrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.08f };
                var imgAttributes = new System.Drawing.Imaging.ImageAttributes();
                imgAttributes.SetColorMatrix(colorMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);

                g.DrawImage(img, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, imgAttributes);

                this.BackgroundImage = bmp;
                this.BackgroundImageLayout = ImageLayout.Tile;
            }
        }
        catch { }
    }
}
