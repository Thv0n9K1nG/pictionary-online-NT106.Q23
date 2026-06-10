namespace Client.UI;

using System.Drawing;
using System.Windows.Forms;
using Client.Utils;
using Siticone.Desktop.UI.WinForms;

public sealed class ExitGameConfirmForm : Form
{
    private readonly SiticoneBorderlessForm _borderlessForm;

    public ExitGameConfirmForm()
    {
        Text = "Xác nhận thoát game";
        Width = 420;
        Height = 210;
        StartPosition = FormStartPosition.CenterParent;

        AppTheme.ApplyDarkForm(this);
        AppTheme.ApplyDoodleBackground(this);

        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        _ = new SiticoneDragControl { TargetControl = this };

        var questionLabel = new Label
        {
            Text = "Bạn có chắc muốn thoát game?",
            Left = 30,
            Top = 35,
            Width = 360,
            Height = 55,
            Font = AppTheme.HeaderFont,
            ForeColor = AppTheme.Text,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(questionLabel);

        var cancelButton = new SiticoneButton
        {
            Text = "Hủy",
            Left = 65,
            Top = 115,
            Width = 140,
            Height = 42,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleSecondaryButton(cancelButton);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(cancelButton);

        var exitButton = new SiticoneButton
        {
            Text = "Thoát game",
            Left = 220,
            Top = 115,
            Width = 140,
            Height = 42,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleDangerButton(exitButton);
        exitButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(exitButton);
    }
}
