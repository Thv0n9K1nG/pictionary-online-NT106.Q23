using System.Drawing;
using System.Windows.Forms;
using Client.Utils;
using Siticone.Desktop.UI.WinForms;

namespace Client.UI;

public sealed class GatewayConnectionForm : Form
{
    private readonly Label _statusLabel;
    private readonly PictureBox _logoBox;
    private readonly SiticoneBorderlessForm _borderlessForm;

    public GatewayConnectionForm()
    {
        Text = "Pictionary Online - Connecting";
        Width = 550;
        Height = 430;
        StartPosition = FormStartPosition.CenterScreen;

        AppTheme.ApplyDarkForm(this);

        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        _ = new SiticoneDragControl { TargetControl = this };

        _logoBox = new PictureBox
        {
            Width = 410,
            Height = 250,
            Left = (ClientSize.Width - 380) / 2,
            Top = 36,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };

        try
        {
            var logoPath = AppTheme.TryGetAssetPath("logo.png");
            if (!string.IsNullOrWhiteSpace(logoPath))
            {
                _logoBox.Image = Image.FromFile(logoPath);
            }
        }
        catch
        {
            // Keep the connection form usable even if the optional logo cannot be loaded.
        }

        _statusLabel = new Label
        {
            Text = "Connecting to Gateway...",
            AutoSize = false,
            Font = AppTheme.NormalFont,
            ForeColor = AppTheme.Primary,
            TextAlign = ContentAlignment.MiddleCenter,
            Left = 55,
            Top = 305,
            Width = 440,
            Height = 32,
            BackColor = Color.Transparent
        };

        var exitGameButton = new SiticoneButton
        {
            Text = "Thoát game",
            Left = 185,
            Top = 355,
            Width = 180,
            Height = 42,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleDangerButton(exitGameButton);
        exitGameButton.Click += (_, _) =>
        {
            using var confirm = new ExitGameConfirmForm();
            if (confirm.ShowDialog(this) == DialogResult.OK)
            {
                Application.Exit();
            }
        };

        Controls.Add(_logoBox);
        Controls.Add(_statusLabel);
        Controls.Add(exitGameButton);

        FormClosed += (_, _) => _logoBox.Image?.Dispose();
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
