namespace GameServer.Engine;

public sealed class GameEngine
{
    public const int RoundSeconds = 60;

    public Task StartGameAsync(CancellationToken cancellationToken = default)
    {
        // TODO: WAITING -> SELECTING_WORD -> DRAWING.
        return Task.CompletedTask;
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
