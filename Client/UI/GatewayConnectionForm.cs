using Client.Utils;
using Siticone.Desktop.UI.WinForms;

namespace Client.UI;

public sealed class GatewayConnectionForm : Form
{
    private readonly Label _statusLabel;
    private readonly SiticoneBorderlessForm _borderlessForm;

    public GatewayConnectionForm()
    {
        Text = "Pictionary Online - Connecting";
        Width = 460;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;

        AppTheme.ApplyDarkForm(this);

        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        _ = new SiticoneDragControl { TargetControl = this };

        var title = new Label
        {
            Text = "Pictionary Online",
            AutoSize = true,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Primary,
            Left = 30,
            Top = 24
        };

        _statusLabel = new Label
        {
            Text = "Connecting to Gateway...",
            AutoSize = false,
            Font = AppTheme.NormalFont,
            ForeColor = AppTheme.Primary,
            TextAlign = ContentAlignment.MiddleCenter,
            Left = 30,
            Top = 245,
            Width = 390,
            Height = 32
        };

        var exitGameButton = new SiticoneButton
        {
            Text = "Thoát game",
            Left = 165,
            Top = 282,
            Width = 130,
            Height = 34,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleDangerButton(exitGameButton);
        exitGameButton.Click += (_, _) => Application.Exit();

        Controls.Add(title);
        Controls.Add(_statusLabel);
        Controls.Add(exitGameButton);
    }

    public void SetStatus(string status, Color color)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(status, color));
            return;
        }

        _statusLabel.Text = status;
        _statusLabel.ForeColor = color;
    }
}
