namespace Client.UI;

using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Client.Utils;
using Siticone.Desktop.UI.WinForms;

public sealed class ResultForm : Form
{
    private readonly SiticoneBorderlessForm _borderlessForm;
    private readonly SiticonePanel _mainPanel;
    private readonly ListBox _resultList;
    private readonly SiticoneButton _continueButton;
    private readonly SiticoneButton _backToLobbyButton;

    public bool BackToLobbyRequested { get; private set; }

    public ResultForm(string title, List<(string Username, int Score)> results)
    {
        Text = title;
        Width = 420;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;

        AppTheme.ApplyDarkForm(this);
        AppTheme.ApplyDoodleBackground(this);

        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        _ = new SiticoneDragControl { TargetControl = this };

        var lblTitle = new Label
        {
            Text = "Result - " + title,
            AutoSize = true,
            Left = 20,
            Top = 18,
            Font = AppTheme.HeaderFont,
            ForeColor = AppTheme.Text,
            BackColor = Color.Transparent
        };
        Controls.Add(lblTitle);

        _mainPanel = new SiticonePanel { Left = 20, Top = 60, Width = 380, Height = 320 };
        AppTheme.StylePanel(_mainPanel);

        _resultList = new ListBox
        {
            Left = 10,
            Top = 10,
            Width = 360,
            Height = 300,
            BorderStyle = BorderStyle.None
        };
        AppTheme.StyleListBox(_resultList);

        foreach (var result in results)
        {
            _resultList.Items.Add($"{result.Username} - {result.Score} điểm");
        }

        _mainPanel.Controls.Add(_resultList);
        Controls.Add(_mainPanel);

        _continueButton = new SiticoneButton
        {
            Text = "Tiếp tục",
            Left = 115,
            Top = 405,
            Width = 190,
            Height = 42,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleSuccessButton(_continueButton);
        _continueButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_continueButton);

        _backToLobbyButton = new SiticoneButton
        {
            Text = "Back to Lobby",
            Left = 115,
            Top = 405,
            Width = 190,
            Height = 42,
            Visible = false,
            Cursor = Cursors.Hand
        };
        AppTheme.StyleDangerButton(_backToLobbyButton);
        _backToLobbyButton.Click += (_, _) =>
        {
            BackToLobbyRequested = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_backToLobbyButton);
    }

    public void EnableBackToLobby()
    {
        _continueButton.Visible = false;
        _backToLobbyButton.Visible = true;
    }
}
