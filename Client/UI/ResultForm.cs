namespace Client.UI;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Client.Utils; // THÊM DÒNG NÀY ĐỂ DÙNG APP_THEME
using Siticone.Desktop.UI.WinForms; // THÊM DÒNG NÀY ĐỂ DÙNG CONTROL BOX VÀ BORDERLESS

public sealed class ResultForm : Form
{
    private readonly SiticoneBorderlessForm _borderlessForm;

    public ResultForm(string title, List<(string Username, int Score)> results)
    {
        Text = title;
        Width = 420;
        Height = 450;
        StartPosition = FormStartPosition.CenterParent; // Hiển thị ngay giữa Form cha (GameForm)

        // CẬP NHẬT: Khởi tạo Dark Theme cho bảng kết quả
        AppTheme.ApplyDarkForm(this);

        // Đồng bộ thiết kế viền bo tròn 15px không cạnh phẳng
        _borderlessForm = new SiticoneBorderlessForm()
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        var dragControl = new SiticoneDragControl { TargetControl = this };
        var exitButton = new SiticoneControlBox
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FillColor = Color.Transparent,
            IconColor = AppTheme.SubText,
            Left = 375,
            Top = 0
        };
        Controls.Add(exitButton);

        // Tiêu đề nổi bật (ví dụ: "Kết quả Vòng" hoặc "Kết quả Chung cuộc")
        var lblTitle = new Label
        {
            Text = "🏆 " + title,
            AutoSize = true,
            Left = 20,
            Top = 18,
            Font = AppTheme.HeaderFont,
            ForeColor = AppTheme.Text,
            BackColor = Color.Transparent
        };
        Controls.Add(lblTitle);

        // CẬP NHẬT: Thay thế Dock.Fill thô bằng Panel đệm viền tinh tế
        var mainPanel = new SiticonePanel { Left = 20, Top = 60, Width = 380, Height = 360 };
        AppTheme.StylePanel(mainPanel);

        var list = new ListBox
        {
            Left = 10,
            Top = 10,
            Width = 360,
            Height = 340,
            BorderStyle = BorderStyle.None
        };

        // Áp dụng theme tối, chữ sáng cho ListBox hiển thị điểm
        AppTheme.StyleListBox(list);

        foreach (var result in results)
        {
            list.Items.Add($"{result.Username} — {result.Score} điểm");
        }

        mainPanel.Controls.Add(list);
        Controls.Add(mainPanel);
    }
}
