using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Siticone.Desktop.UI.WinForms;

namespace Client.Utils;

public static class AppTheme
{
    // Màu Primary (Nút Tạo, Vào) sang Xanh Cổ Vịt đậm cực kỳ sang trọng
    public static readonly Color Primary =
        Color.FromArgb(43, 140, 128);

    public static readonly Color PrimaryDark =
        Color.FromArgb(33, 110, 100);

    // Màu Danger (Nút Logout) sang Đỏ Coral/Hồng Cam mềm mại
    public static readonly Color Danger =
        Color.FromArgb(238, 108, 77);

    // Màu Success (Nút Bắt đầu) sang Xanh Lục bảo nhẹ
    public static readonly Color Success =
        Color.FromArgb(82, 183, 136);

    public static readonly Color Warning =
        Color.FromArgb(243, 156, 18);

    // Nền chính của Form (Màu Xanh Mint dịu mắt)
    public static readonly Color DarkBg =
        Color.FromArgb(146, 227, 213);

    // Nền của các Khung (Panel), TextBox, ListBox - Màu Trắng tinh khôi
    public static readonly Color PanelBg =
        Color.White;

    // Màu Viền (Border) - Xanh Mint đậm hơn một xíu để kẻ viền
    public static readonly Color Border =
        Color.FromArgb(115, 205, 195);

    // Chữ chính màu Xám Đậm
    public static readonly Color Text =
        Color.FromArgb(85, 95, 105);

    // Chữ phụ màu Xám Nhạt
    public static readonly Color SubText =
        Color.FromArgb(150, 160, 170);

    // GIỮ NGUYÊN: Phông chữ Segoe UI mặc định
    public static Font TitleFont =>
        new("Segoe UI", 18, FontStyle.Bold);

    public static Font HeaderFont =>
        new("Segoe UI", 12, FontStyle.Bold);

    public static Font NormalFont =>
        new("Segoe UI", 10);

    public static void ApplyDarkForm(Form form)
    {
        form.BackColor = DarkBg;
        form.ForeColor = Text;
    }

    public static void ApplyDoodleBackground(Form form)
    {
        try
        {
            var bgPath = TryGetAssetPath("doodle_bg.png", "doodle_bg.jpg");
            if (string.IsNullOrWhiteSpace(bgPath))
            {
                return;
            }

            using var img = Image.FromFile(bgPath);
            var bmp = new Bitmap(img.Width, img.Height);
            using var g = Graphics.FromImage(bmp);

            g.Clear(DarkBg);

            var colorMatrix = new ColorMatrix { Matrix33 = 0.08f };
            using var imgAttributes = new ImageAttributes();
            imgAttributes.SetColorMatrix(
                colorMatrix,
                ColorMatrixFlag.Default,
                ColorAdjustType.Bitmap);

            g.DrawImage(
                img,
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                0,
                0,
                img.Width,
                img.Height,
                GraphicsUnit.Pixel,
                imgAttributes);

            var oldBackground = form.BackgroundImage;
            form.BackgroundImage = bmp;
            form.BackgroundImageLayout = ImageLayout.Tile;
            oldBackground?.Dispose();
        }
        catch
        {
            // Decorative only; keep the form usable if the asset cannot be loaded.
        }
    }

    public static string? TryGetAssetPath(params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            foreach (var basePath in GetAssetSearchRoots())
            {
                var path = Path.GetFullPath(Path.Combine(basePath, fileName));
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> GetAssetSearchRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Directory.GetCurrentDirectory();
        yield return Path.Combine(Directory.GetCurrentDirectory(), "Client");
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }

    public static void ApplyCornerLogo(Form form, string corner = "TopLeft")
    {
        var picCornerLogo = new PictureBox
        {
            Size = new Size(90, 55),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        try
        {
            var logoPath = TryGetAssetPath("logo.png");
            if (!string.IsNullOrWhiteSpace(logoPath))
            {
                picCornerLogo.Image = Image.FromFile(logoPath);
            }
        }
        catch { }

        switch (corner)
        {
            case "TopLeft":
                picCornerLogo.Left = 15;
                picCornerLogo.Top = 12;
                picCornerLogo.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                break;

            case "TopRight":
                picCornerLogo.Left = form.Width - picCornerLogo.Width - 60;
                picCornerLogo.Top = 10;
                picCornerLogo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                break;

            case "BottomRight":
                picCornerLogo.Left = form.Width - picCornerLogo.Width - 15;
                picCornerLogo.Top = form.Height - picCornerLogo.Height - 15;
                picCornerLogo.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                break;

            case "BottomLeft":
                picCornerLogo.Left = 15;
                picCornerLogo.Top = form.Height - picCornerLogo.Height - 15;
                picCornerLogo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                break;
        }

        form.Controls.Add(picCornerLogo);
        picCornerLogo.BringToFront();
    }

    // ==========================================
    // KHU VỰC STYLE ĐÃ FIX LỖI VIỀN & HOVER CHỮ
    // ==========================================

    public static void StylePrimaryButton(SiticoneButton btn)
    {
        btn.FillColor = Primary;
        btn.ForeColor = Color.White;
        btn.HoverState.ForeColor = Color.White; // Giữ chữ trắng khi hover
        btn.BorderRadius = 8;
        btn.Font = HeaderFont;
        btn.UseTransparentBackground = true;
    }

    public static void StyleSuccessButton(SiticoneButton btn)
    {
        btn.FillColor = Success;
        btn.ForeColor = Color.White;
        btn.HoverState.ForeColor = Color.White; // Giữ chữ trắng khi hover
        btn.BorderRadius = 8;
        btn.Font = HeaderFont;
        btn.UseTransparentBackground = true;
    }

    public static void StyleDangerButton(SiticoneButton btn)
    {
        btn.FillColor = Danger;
        btn.ForeColor = Color.White;
        btn.HoverState.ForeColor = Color.White; // Giữ chữ trắng khi hover
        btn.BorderRadius = 8;
        btn.Font = HeaderFont;
        btn.UseTransparentBackground = true;
    }

    // Nút phụ (Dùng cho nút "Làm mới")
    public static void StyleSecondaryButton(SiticoneButton btn)
    {
        btn.FillColor = Border;
        btn.ForeColor = Color.White;
        btn.HoverState.ForeColor = Color.White;
        btn.BorderRadius = 8;
        btn.Font = HeaderFont;
        btn.UseTransparentBackground = true;
    }

    public static void StyleTextBox(SiticoneTextBox box)
    {
        box.FillColor = PanelBg;
        box.ForeColor = Text;
        box.BorderColor = Border;
        box.BorderRadius = 8;
        box.PlaceholderForeColor = SubText; // Làm rõ chữ gợi ý
        box.BackColor = Color.Transparent;  // Fix viền trắng ở 4 góc bo
    }

    public static void StylePanel(SiticonePanel panel)
    {
        panel.FillColor = PanelBg;
        panel.BorderColor = Border;
        panel.BorderThickness = 1;
        panel.BorderRadius = 10;
        panel.BackColor = Color.Transparent; // Fix viền trắng ở 4 góc bo
        panel.UseTransparentBackground = true;
    }

    public static void StyleLabel(Label lbl)
    {
        lbl.ForeColor = Text;
        lbl.Font = NormalFont;
        lbl.BackColor = Color.Transparent;
        lbl.UseCompatibleTextRendering = true; // Fix nét chữ bị răng cưa
    }

    public static void StyleListBox(ListBox list)
    {
        list.BackColor = PanelBg;
        list.ForeColor = Text;
        list.BorderStyle = BorderStyle.None;
        list.Font = NormalFont;
    }

    public static void StyleListView(ListView list)
    {
        list.BackColor = PanelBg;
        list.ForeColor = Text;
        list.BorderStyle = BorderStyle.None;
        list.GridLines = false;
        list.Font = NormalFont;
    }
}
