using Shared.Models;

namespace GameServer.Services;

public sealed class MatchResultReporter
{
    public Task ReportAsync(MatchResult result, CancellationToken cancellationToken = default)
    {
        // TODO: Send MATCH_RESULT to Gateway.
        return Task.CompletedTask;
    }
}
