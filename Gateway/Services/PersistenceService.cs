namespace Gateway.Services;

public sealed class PersistenceService
{
    public Task SaveMatchResultAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Insert Matches, MatchPlayers and update PlayerStats in one transaction.
        return Task.CompletedTask;
    }
}
