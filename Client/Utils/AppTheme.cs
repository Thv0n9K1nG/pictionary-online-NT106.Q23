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
