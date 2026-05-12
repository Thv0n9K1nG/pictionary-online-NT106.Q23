namespace Client.UI;

public sealed class ResultForm : Form
{
    public ResultForm(
        string title,
        List<(string Username, int Score)> results)
    {
        Text = title;

        Width = 400;
        Height = 400;

        var list = new ListBox
        {
            Dock = DockStyle.Fill
        };

        foreach (var result in results)
        {
            list.Items.Add(
                $"{result.Username} - {result.Score}");
        }

        Controls.Add(list);
    }
}
