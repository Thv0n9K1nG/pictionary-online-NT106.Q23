using System;
using System.Drawing;
using System.Windows.Forms;
using Client.Utils;
using Siticone.Desktop.UI.WinForms;

namespace Client.UI;

public sealed class SplashForm : Form
{
    private readonly System.Windows.Forms.Timer _loadingTimer = new();
    private readonly SiticoneProgressBar _progressBar = new();
    private int _progressValue;

    public SplashForm()
    {
        Text = "Pictionary Online - Loading";
        Width = 550;
        Height = 380;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;

        AppTheme.ApplyDarkForm(this);

        _ = new SiticoneBorderlessForm { ContainerControl = this, BorderRadius = 15 };

        var picLogo = new PictureBox
        {
            Width = 410,
            Height = 250,
            Left = (ClientSize.Width - 380) / 2,
            Top = 60,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        try
        {
            var logoPath = AppTheme.TryGetAssetPath("logo.png");
            if (!string.IsNullOrWhiteSpace(logoPath))
            {
                picLogo.Image = Image.FromFile(logoPath);
            }
            else
            {
                var lblFallback = new Label
                {
                    Text = "PICTIONARY\nONLINE",
                    Font = AppTheme.TitleFont,
                    ForeColor = AppTheme.Primary,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                Controls.Add(lblFallback);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logo load failed: {ex.Message}");
        }

        Controls.Add(picLogo);

        _progressBar.Height = 6;
        _progressBar.Dock = DockStyle.Bottom;
        _progressBar.FillColor = AppTheme.Border;
        _progressBar.ProgressColor = AppTheme.Primary;
        _progressBar.ProgressColor2 = AppTheme.Success;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        Controls.Add(_progressBar);

        SetupDoodleBackground();

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
            Close();
        }
        else
        {
            _progressBar.Value = _progressValue;
        }
    }

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
                imgAttributes.SetColorMatrix(
                    colorMatrix,
                    System.Drawing.Imaging.ColorMatrixFlag.Default,
                    System.Drawing.Imaging.ColorAdjustType.Bitmap);

                g.DrawImage(
                    img,
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    0,
                    0,
                    img.Width,
                    img.Height,
                    GraphicsUnit.Pixel,
                    imgAttributes);

                BackgroundImage = bmp;
                BackgroundImageLayout = ImageLayout.Tile;
            }
        }
        catch
        {
        }
    }
}
