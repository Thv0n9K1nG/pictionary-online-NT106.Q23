namespace Client.UI;

public sealed class WordSelectionForm : Form
{
    public string SelectedWord { get; private set; } = "";

    public WordSelectionForm(List<string> words)
    {
        Text = "Choose a word";

        Width = 300;
        Height = 350;

        int top = 20;

        foreach (var word in words)
        {
            var btn = new Button
            {
                Text = word,
                Width = 220,
                Height = 40,
                Left = 30,
                Top = top
            };

            btn.Click += (_, _) =>
            {
                SelectedWord = word;

                DialogResult = DialogResult.OK;

                Close();
            };

            Controls.Add(btn);

            top += 50;
        }
    }
}
