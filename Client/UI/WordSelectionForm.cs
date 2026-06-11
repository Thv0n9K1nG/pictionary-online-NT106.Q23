namespace Client.UI;

using System.Collections.Generic;
using System.Windows.Forms;
using Client.Utils;
using Siticone.Desktop.UI.WinForms;

public sealed class WordSelectionForm : Form
{
    public string SelectedWord { get; private set; } = "";
    private readonly SiticoneBorderlessForm _borderlessForm;

    public WordSelectionForm(List<string> words)
    {
        Text = "Choose a word";
        Width = 300;
        StartPosition = FormStartPosition.CenterParent;

        AppTheme.ApplyDarkForm(this);

        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        _ = new SiticoneDragControl { TargetControl = this };

        var lblTitle = new Label
        {
            Text = "Chọn một từ để vẽ:",
            AutoSize = true,
            Left = 30,
            Top = 22
        };
        AppTheme.StyleLabel(lblTitle);
        lblTitle.Font = AppTheme.HeaderFont;
        Controls.Add(lblTitle);

        var top = 65;
        const int buttonWidth = 240;
        const int buttonHeight = 42;

        foreach (var word in words)
        {
            var button = ThemeExtensions.CreatePrimaryButton(
                word.ToUpperInvariant(),
                30,
                top,
                buttonWidth,
                buttonHeight);

            button.Cursor = Cursors.Hand;
            button.Click += (_, _) =>
            {
                SelectedWord = word;
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(button);
            top += 52;
        }

        var backButton = new SiticoneButton
        {
            Text = "Quay lại",
            Left = 30,
            Top = top + 5,
            Width = buttonWidth,
            Height = buttonHeight,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleDangerButton(backButton);
        backButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(backButton);

        Height = top + 70;
    }
}
