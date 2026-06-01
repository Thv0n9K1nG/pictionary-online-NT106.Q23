using System.Drawing;
using System.Windows.Forms;
using Siticone.Desktop.UI.WinForms;

namespace Client.Utils;

public static class AppTheme
{
	public static readonly Color Primary =
		Color.FromArgb(124, 92, 255);

	public static readonly Color PrimaryDark =
		Color.FromArgb(91, 61, 245);

	public static readonly Color Success =
		Color.FromArgb(46, 204, 113);

	public static readonly Color Danger =
		Color.FromArgb(231, 76, 60);

	public static readonly Color Warning =
		Color.FromArgb(243, 156, 18);

	public static readonly Color DarkBg =
		Color.FromArgb(21, 26, 40);

	public static readonly Color PanelBg =
		Color.FromArgb(31, 41, 55);

	public static readonly Color Border =
		Color.FromArgb(51, 65, 85);

	public static readonly Color Text =
		Color.White;

	public static readonly Color SubText =
		Color.FromArgb(160, 174, 192);

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
    public static void ApplyCornerLogo(Form form, string corner = "TopLeft")
    {
        var picCornerLogo = new PictureBox
        {
            Size = new Size(90, 55), // Kích thước logo thu nhỏ ở góc
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        // Load ảnh tương đối từ thư mục gốc (đã cấu hình từ bước trước)
        try
        {
            if (System.IO.File.Exists("logo.png"))
            {
                picCornerLogo.Image = Image.FromFile("logo.png");
            }
        }
        catch { /* Bỏ qua nếu lỗi */ }

        // Tính toán vị trí dựa theo góc bạn chọn
        switch (corner)
        {
            case "TopLeft":
                picCornerLogo.Left = 15;
                picCornerLogo.Top = 12;
                picCornerLogo.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                break;

            case "TopRight":
                // Cách cạnh phải 60px (để tránh đè vào nút Exit của Siticone thường cách 40-50px)
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

        // Add vào form
        form.Controls.Add(picCornerLogo);

        // MẸO WINFORMS: Ép logo luôn nổi lên trên cùng, không bị các Panel hay Background đè mất
        picCornerLogo.BringToFront();
    }
    public static void StylePrimaryButton(
		SiticoneButton btn)
	{
		btn.FillColor = Primary;
		btn.ForeColor = Text;
		btn.BorderRadius = 8;
		btn.Font = HeaderFont;
	}

	public static void StyleSuccessButton(
		SiticoneButton btn)
	{
		btn.FillColor = Success;
		btn.ForeColor = Text;
		btn.BorderRadius = 8;
		btn.Font = HeaderFont;
	}

	public static void StyleDangerButton(
		SiticoneButton btn)
	{
		btn.FillColor = Danger;
		btn.ForeColor = Text;
		btn.BorderRadius = 8;
		btn.Font = HeaderFont;
	}

	public static void StyleTextBox(
		SiticoneTextBox box)
	{
		box.FillColor = PanelBg;
		box.ForeColor = Text;
		box.BorderColor = Border;
		box.BorderRadius = 8;
	}

	public static void StylePanel(
		SiticonePanel panel)
	{
		panel.FillColor = PanelBg;
		panel.BorderColor = Border;
		panel.BorderThickness = 1;
		panel.BorderRadius = 10;
	}

	public static void StyleLabel(
		Label lbl)
	{
		lbl.ForeColor = Text;
		lbl.Font = NormalFont;
	}

	public static void StyleListBox(
		ListBox list)
	{
		list.BackColor = PanelBg;
		list.ForeColor = Text;
		list.BorderStyle = BorderStyle.None;
		list.Font = NormalFont;
	}

	public static void StyleListView(
		ListView list)
	{
		list.BackColor = PanelBg;
		list.ForeColor = Text;
		list.BorderStyle = BorderStyle.None;
		list.GridLines = false;
		list.Font = NormalFont;
	}
}
