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
    private readonly SiticoneButton _backToLobbyButton;

    public bool BackToLobbyRequested { get; private set; }

    public ResultForm(string title, List<(string Username, int Score)> results)
    {
        Text = title;
        Width = 420;
        Height = 450;
        StartPosition = FormStartPosition.CenterParent;

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
            Left = 375,
            Top = 0
        };
        Controls.Add(exitButton);

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

        _mainPanel = new SiticonePanel { Left = 20, Top = 60, Width = 380, Height = 360 };
        AppTheme.StylePanel(_mainPanel);

        _resultList = new ListBox
        {
            Left = 10,
            Top = 10,
            Width = 360,
            Height = 340,
            BorderStyle = BorderStyle.None
        };
        AppTheme.StyleListBox(_resultList);

        foreach (var result in results)
        {
            _resultList.Items.Add($"{result.Username} - {result.Score} diem");
        }

        _mainPanel.Controls.Add(_resultList);
        Controls.Add(_mainPanel);

        _backToLobbyButton = new SiticoneButton
        {
            Text = "Back to Lobby",
            Left = 115,
            Top = 430,
            Width = 190,
            Height = 42,
            Visible = false,
            Cursor = Cursors.Hand
        };
        AppTheme.StylePrimaryButton(_backToLobbyButton);
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
        Height = 520;
        _mainPanel.Height = 340;
        _resultList.Height = 320;
        _backToLobbyButton.Visible = true;
    }
}
