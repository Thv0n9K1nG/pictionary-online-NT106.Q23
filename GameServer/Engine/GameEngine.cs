using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Engine;

public sealed class GameEngine
{
    public const int RoundSeconds = 60;

    // Các event để GameRoom hoặc test script bắt và xử lý
    public event Action<int>? OnTimerUpdated;
    public event Action? OnHintTriggered;
    public event Action? OnRoundEnded;

    public Task StartGameAsync(CancellationToken cancellationToken = default)
    {
        // TODO: WAITING -> SELECTING_WORD -> DRAWING.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Chạy vòng lặp đếm ngược 60 giây cho một lượt vẽ
    /// </summary>
    public async Task RunRoundTimerAsync(CancellationToken cancellationToken = default)
    {
        int remainingSeconds = RoundSeconds;

        while (remainingSeconds > 0 && !cancellationToken.IsCancellationRequested)
        {
            // 1. Phát event cập nhật UI
            OnTimerUpdated?.Invoke(remainingSeconds);

            // 2. Kích hoạt Hint tự động ở giây 40 và 20
            if (remainingSeconds == 40 || remainingSeconds == 20)
            {
                OnHintTriggered?.Invoke();
            }

            // 3. Đợi 1 giây, hỗ trợ hủy ngang
            try
            {
                await Task.Delay(1000, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break; // Vòng chơi bị hủy sớm
            }

            remainingSeconds--;
        }

        // Nếu hết giờ tự nhiên
        if (!cancellationToken.IsCancellationRequested)
        {
            OnTimerUpdated?.Invoke(0);
            OnRoundEnded?.Invoke();
        }
    }

    public int CalculateGuessScore(int elapsedSeconds)
    {
        return Math.Max((int)(100 - elapsedSeconds * 1.5), 10);
    }

    public int CalculateDrawerScore(int correctGuessers)
    {
        return correctGuessers * 20;
    }
}