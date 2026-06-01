namespace Client.UI;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Client.Utils; // Khai báo để dùng AppTheme và ThemeExtensions
using Siticone.Desktop.UI.WinForms;

public sealed class WordSelectionForm : Form
{
    public string SelectedWord { get; private set; } = "";
    private readonly SiticoneBorderlessForm _borderlessForm;

    public WordSelectionForm(List<string> words)
    {
        Text = "Choose a word";
        Width = 300;
        StartPosition = FormStartPosition.CenterParent; // Hiển thị ngay giữa GameForm

        // 1. Áp dụng nền tối cho Form từ AppTheme
        AppTheme.ApplyDarkForm(this);

        // 2. Thiết kế phẳng không viền và bo góc 15px chuẩn UI dự án
        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        var dragControl = new SiticoneDragControl { TargetControl = this };

        // Nút X đóng nhanh góc phải (Sử dụng màu SubText từ AppTheme)
        var exitButton = new SiticoneControlBox
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FillColor = Color.Transparent,
            IconColor = AppTheme.SubText,
            Left = 255,
            Top = 0
        };
        Controls.Add(exitButton);

        // Tiêu đề gợi ý
        var lblTitle = new Label
        {
            Text = "💡 Chọn một từ để vẽ:",
            AutoSize = true,
            Left = 30,
            Top = 22
        };
        AppTheme.StyleLabel(lblTitle); // Áp dụng style chữ chuẩn của hệ thống
        lblTitle.Font = AppTheme.HeaderFont; // Ghi đè lên thành font chữ đậm cho nổi bật
        Controls.Add(lblTitle);

        // Vị trí Y bắt đầu rải các nút bấm chọn từ
        int top = 65;
        int buttonWidth = 240;
        int buttonHeight = 42;

        foreach (var word in words)
        {
            // 3. Sử dụng hàm khởi tạo nhanh từ ThemeExtensions của bạn
            var btn = ThemeExtensions.CreatePrimaryButton(
                word.ToUpper(), // Viết hoa từ khóa giúp người chơi dễ nhìn
                30,
                top,
                buttonWidth,
                buttonHeight
            );

            btn.Cursor = Cursors.Hand;

            btn.Click += (_, _) =>
            {
                SelectedWord = word;
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(btn);

            // Khoảng cách giữa các nút là 10px (42px chiều cao + 10px đệm = 52px)
            top += 52;
        }

        // 4. Tự động tính toán chiều cao Form vừa khít với số lượng từ nhận về
        Height = top + 20;
    }
}
