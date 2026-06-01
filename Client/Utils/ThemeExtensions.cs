using Siticone.Desktop.UI.WinForms;

namespace Client.Utils;

public static class ThemeExtensions
{
    public static SiticonePanel CreatePanel(
        int x,
        int y,
        int width,
        int height)
    {
        var panel = new SiticonePanel
        {
            Left = x,
            Top = y,
            Width = width,
            Height = height
        };

        AppTheme.StylePanel(panel);

        return panel;
    }

    public static SiticoneButton CreatePrimaryButton(
        string text,
        int x,
        int y,
        int width,
        int height)
    {
        var btn = new SiticoneButton
        {
            Text = text,
            Left = x,
            Top = y,
            Width = width,
            Height = height
        };

        AppTheme.StylePrimaryButton(btn);

        return btn;
    }

    public static SiticoneButton CreateSuccessButton(
        string text,
        int x,
        int y,
        int width,
        int height)
    {
        var btn = new SiticoneButton
        {
            Text = text,
            Left = x,
            Top = y,
            Width = width,
            Height = height
        };

        AppTheme.StyleSuccessButton(btn);

        return btn;
    }
}
