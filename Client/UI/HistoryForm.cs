namespace Client.UI;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Client.Services;
using Client.State;
using Client.Utils;
using Shared.Models;
using Siticone.Desktop.UI.WinForms;

public sealed class HistoryForm : Form
{
    private const int DefaultHistoryLimit = 30;

    // Vị trí/kích thước 4 thông số trong stat panel: chỉnh các hằng số này nếu bạn muốn tinh lại UI.
    private const int StatPanelLeft = 20;
    private const int StatPanelTop = 65;
    private const int StatPanelWidth = 650;
    private const int StatPanelHeight = 108;
    private const int StatFirstLeft = 18;
    private const int StatTop = 18;
    private const int StatColumnGap = 158;
    private const int StatLabelWidth = 148;
    private const int StatLabelHeight = 72;

    private readonly ClientState _state;
    private readonly SocketService _socketService;
    private readonly MessageDispatcher _dispatcher;
    private readonly ListView _historyList = new();
    private readonly TextBox _detailBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _totalMatchesLabel = new();
    private readonly Label _winsLabel = new();
    private readonly Label _scoreLabel = new();
    private readonly Label _correctGuessesLabel = new();
    private readonly SiticoneButton _refreshButton = new();
    private readonly SiticoneButton _backButton = new();
    private readonly List<MatchResult> _matches = [];

    private SiticoneBorderlessForm _borderlessForm;

    public HistoryForm(ClientState state, SocketService socketService, MessageDispatcher dispatcher)
    {
        _state = state;
        _socketService = socketService;
        _dispatcher = dispatcher;

        Text = "Pictionary Online - Match History";
        Width = 840;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;

        AppTheme.ApplyDarkForm(this);
        AppTheme.ApplyDoodleBackground(this);
        _borderlessForm = new SiticoneBorderlessForm
        {
            ContainerControl = this,
            BorderRadius = 15
        };

        _ = new SiticoneDragControl { TargetControl = this };

        var titleLabel = new Label
        {
            Text = "Lịch sử đấu",
            AutoSize = true,
            Left = 20,
            Top = 18,
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Text,
            BackColor = Color.Transparent
        };

        _refreshButton.Text = "Làm mới";
        _refreshButton.Left = 690;
        _refreshButton.Top = 70;
        _refreshButton.Width = 120;
        _refreshButton.Height = 38;
        _refreshButton.Cursor = Cursors.Hand;
        AppTheme.StyleSecondaryButton(_refreshButton);

        _backButton.Text = "Quay lại";
        _backButton.Left = 690;
        _backButton.Top = 120;
        _backButton.Width = 120;
        _backButton.Height = 38;
        _backButton.Cursor = Cursors.Hand;
        AppTheme.StyleDangerButton(_backButton);

        var statsPanel = new SiticonePanel { Left = StatPanelLeft, Top = StatPanelTop, Width = StatPanelWidth, Height = StatPanelHeight };
        AppTheme.StylePanel(statsPanel);

        ConfigureStatsLabel(_totalMatchesLabel, "Trận đã chơi: ...", StatFirstLeft, StatTop);
        ConfigureStatsLabel(_winsLabel, "Trận thắng: ...", StatFirstLeft + StatColumnGap, StatTop);
        ConfigureStatsLabel(_scoreLabel, "Tổng điểm: ...", StatFirstLeft + StatColumnGap * 2, StatTop);
        ConfigureStatsLabel(_correctGuessesLabel, "Đoán đúng: ...", StatFirstLeft + StatColumnGap * 3, StatTop);
        statsPanel.Controls.AddRange([
            _totalMatchesLabel,
            _winsLabel,
            _scoreLabel,
            _correctGuessesLabel
        ]);

        _statusLabel.Left = 20;
        _statusLabel.Top = 184;
        _statusLabel.Width = 790;
        _statusLabel.Height = 24;
        _statusLabel.Text = "Đang tải lịch sử đấu...";
        AppTheme.StyleLabel(_statusLabel);
        _statusLabel.Font = AppTheme.NormalFont;

        var historyPanel = new SiticonePanel { Left = 20, Top = 214, Width = 790, Height = 260 };
        AppTheme.StylePanel(historyPanel);

        _historyList.Left = 10;
        _historyList.Top = 10;
        _historyList.Width = 770;
        _historyList.Height = 240;
        _historyList.View = View.Details;
        _historyList.FullRowSelect = true;
        _historyList.MultiSelect = false;
        _historyList.HideSelection = false;
        _historyList.Columns.Add("Thời gian", 150);
        _historyList.Columns.Add("Phòng", 90);
        _historyList.Columns.Add("Kết quả", 90);
        _historyList.Columns.Add("Điểm", 80);
        _historyList.Columns.Add("Người chơi", 430);
        AppTheme.StyleListView(_historyList);
        historyPanel.Controls.Add(_historyList);

        var detailPanel = new SiticonePanel { Left = 20, Top = 490, Width = 790, Height = 82 };
        AppTheme.StylePanel(detailPanel);

        _detailBox.Left = 10;
        _detailBox.Top = 8;
        _detailBox.Width = 770;
        _detailBox.Height = 64;
        _detailBox.Multiline = true;
        _detailBox.ReadOnly = true;
        _detailBox.BorderStyle = BorderStyle.None;
        _detailBox.BackColor = AppTheme.PanelBg;
        _detailBox.ForeColor = AppTheme.Text;
        _detailBox.Font = AppTheme.NormalFont;
        _detailBox.ScrollBars = ScrollBars.Vertical;
        detailPanel.Controls.Add(_detailBox);

        Controls.AddRange([
            titleLabel,
            statsPanel,
            _refreshButton,
            _backButton,
            _statusLabel,
            historyPanel,
            detailPanel
        ]);

        AppTheme.ApplyCornerLogo(this, "TopRight");

        _dispatcher.MatchHistoryReceived += OnMatchHistoryReceived;
        _dispatcher.PlayerStatsReceived += OnPlayerStatsReceived;
        _dispatcher.SystemMessageReceived += OnSystemMessageReceived;
        FormClosed += (_, _) =>
        {
            _dispatcher.MatchHistoryReceived -= OnMatchHistoryReceived;
            _dispatcher.PlayerStatsReceived -= OnPlayerStatsReceived;
            _dispatcher.SystemMessageReceived -= OnSystemMessageReceived;
        };

        _refreshButton.Click += async (_, _) => await RequestHistoryAndStatsAsync();
        _backButton.Click += (_, _) => Close();
        _historyList.SelectedIndexChanged += (_, _) => RenderSelectedMatch();
        Load += async (_, _) => await RequestHistoryAndStatsAsync();
    }

    private static void ConfigureStatsLabel(Label label, string text, int left, int top)
    {
        label.Text = text;
        label.Left = left;
        label.Top = top;
        label.Width = StatLabelWidth;
        label.Height = StatLabelHeight;
        label.Font = new Font(AppTheme.NormalFont.FontFamily, 10, FontStyle.Bold);
        label.ForeColor = AppTheme.Text;
        label.BackColor = Color.Transparent;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private async Task RequestHistoryAndStatsAsync()
    {
        if (string.IsNullOrWhiteSpace(_state.SessionId))
        {
            _statusLabel.Text = "Bạn cần đăng nhập trước khi xem lịch sử đấu.";
            _statusLabel.ForeColor = AppTheme.Danger;
            return;
        }

        _refreshButton.Enabled = false;
        _statusLabel.Text = "Đang tải lịch sử đấu...";
        _statusLabel.ForeColor = AppTheme.Text;

        try
        {
            await _socketService.SendAsync(GameMessageFactory.GetMatchHistory(_state.SessionId, DefaultHistoryLimit));
            await _socketService.SendAsync(GameMessageFactory.GetPlayerStats(_state.SessionId));
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Không thể tải lịch sử: {ex.Message}";
            _statusLabel.ForeColor = AppTheme.Danger;
            _refreshButton.Enabled = true;
        }
    }

    private void OnMatchHistoryReceived(IReadOnlyList<MatchResult> history)
    {
        RunOnUi(() => RenderHistory(history));
    }

    private void OnPlayerStatsReceived(PlayerStatsResult stats)
    {
        RunOnUi(() => RenderStats(stats));
    }

    private void OnSystemMessageReceived(string message)
    {
        if (_refreshButton.Enabled)
        {
            return;
        }

        RunOnUi(() =>
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = AppTheme.Danger;
            _refreshButton.Enabled = true;
        });
    }

    private void RenderHistory(IReadOnlyList<MatchResult> history)
    {
        _matches.Clear();
        _matches.AddRange(history);
        _historyList.Items.Clear();
        _detailBox.Clear();

        if (_matches.Count == 0)
        {
            _statusLabel.Text = "Chưa có trận đấu nào được lưu cho tài khoản này.";
            _statusLabel.ForeColor = AppTheme.Text;
            _refreshButton.Enabled = true;
            return;
        }

        foreach (var match in _matches)
        {
            var currentPlayer = FindCurrentPlayer(match);
            var resultText = IsWin(match, currentPlayer) ? "Thắng" : "Thua";
            var scoreText = currentPlayer is not null
                ? currentPlayer.FinalScore.ToString()
                : ReadScore(match).ToString();
            var players = match.Players.Count > 0
                ? string.Join(", ", match.Players.Select(player => $"{player.DisplayName} ({player.FinalScore})"))
                : string.Join(", ", match.FinalScores.Select(score => $"{score.Key} ({score.Value})"));

            var item = new ListViewItem([
                FormatDate(match.EndedAt == DateTimeOffset.MinValue ? match.StartedAt : match.EndedAt),
                match.RoomCode,
                resultText,
                scoreText,
                players
            ])
            {
                Tag = match
            };

            _historyList.Items.Add(item);
        }

        _statusLabel.Text = $"Đã tải {_matches.Count} trận gần nhất.";
        _statusLabel.ForeColor = AppTheme.Text;
        _refreshButton.Enabled = true;

        if (_historyList.Items.Count > 0)
        {
            _historyList.Items[0].Selected = true;
        }
    }

    private void RenderStats(PlayerStatsResult stats)
    {
        var winRate = stats.TotalMatches == 0
            ? 0
            : (int)Math.Round((double)stats.Wins * 100 / stats.TotalMatches);

        _totalMatchesLabel.Text = $"Trận đã chơi:{Environment.NewLine}{stats.TotalMatches}";
        _winsLabel.Text = $"Trận thắng:{Environment.NewLine}{stats.Wins} ({winRate}%)";
        _scoreLabel.Text = $"Tổng điểm:{Environment.NewLine}{stats.TotalScore}";
        _correctGuessesLabel.Text = $"Đoán đúng:{Environment.NewLine}{stats.CorrectGuesses}";
    }

    private void RenderSelectedMatch()
    {
        if (_historyList.SelectedItems.Count == 0 || _historyList.SelectedItems[0].Tag is not MatchResult match)
        {
            return;
        }

        var currentPlayer = FindCurrentPlayer(match);
        var winnerName = match.Players.FirstOrDefault(player => player.IsWinner)?.DisplayName
            ?? match.Players.FirstOrDefault(player => player.PlayerId == match.WinnerId)?.DisplayName
            ?? match.WinnerId;
        var builder = new StringBuilder();

        builder.Append($"Phòng {match.RoomCode} | ");
        builder.Append($"{FormatDate(match.StartedAt)} - {FormatDate(match.EndedAt)} | ");
        builder.Append($"Người thắng: {winnerName}");

        if (currentPlayer is not null)
        {
            builder.AppendLine();
            builder.Append($"Điểm của bạn: {currentPlayer.FinalScore} ");
            builder.Append($"(vẽ {currentPlayer.DrawScore}, đoán {currentPlayer.GuessScore}, đúng {currentPlayer.CorrectGuesses})");
        }

        if (match.Players.Count > 0)
        {
            builder.AppendLine();
            builder.Append(string.Join(" | ", match.Players.Select(player =>
                $"{player.DisplayName}: {player.FinalScore}{(player.IsWinner ? " thắng" : string.Empty)}")));
        }

        _detailBox.Text = builder.ToString();
    }

    private MatchPlayerResult? FindCurrentPlayer(MatchResult match)
    {
        return match.Players.FirstOrDefault(player => player.PlayerId == _state.PlayerId);
    }

    private bool IsWin(MatchResult match, MatchPlayerResult? currentPlayer)
    {
        return currentPlayer?.IsWinner == true
            || (!string.IsNullOrWhiteSpace(_state.PlayerId)
                && string.Equals(match.WinnerId, _state.PlayerId, StringComparison.OrdinalIgnoreCase));
    }

    private int ReadScore(MatchResult match)
    {
        if (!string.IsNullOrWhiteSpace(_state.PlayerId)
            && match.FinalScores.TryGetValue(_state.PlayerId, out var score))
        {
            return score;
        }

        return 0;
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return value == DateTimeOffset.MinValue
            ? "N/A"
            : value.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    private void RunOnUi(Action action)
    {
        if (!IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }
}
