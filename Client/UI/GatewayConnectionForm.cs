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

        var exitButton = new SiticoneControlBox
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FillColor = Color.Transparent,
            IconColor = AppTheme.SubText,
            Left = 410,
            Top = 0
        };

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

        Controls.Add(exitButton);
        Controls.Add(title);
        Controls.Add(_statusLabel);
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
