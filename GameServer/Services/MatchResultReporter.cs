using Shared.Models;
using Shared.Protocol;

namespace GameServer.Services;

public sealed class MatchResultReporter
{
    private readonly Func<InternalMessageType, object, CancellationToken, Task>? _sendInternalAsync;

    public MatchResultReporter()
    {
    }

    public MatchResultReporter(Func<InternalMessageType, object, CancellationToken, Task> sendInternalAsync)
    {
        _sendInternalAsync = sendInternalAsync;
    }

    public Task ReportAsync(MatchResult result, CancellationToken cancellationToken = default)
    {
        if (_sendInternalAsync is null)
        {
            return Task.CompletedTask;
        }

        // Member D: GAME_END persistence is reported once to Gateway, not written by GameServer.
        return _sendInternalAsync(InternalMessageType.MatchResult, result, cancellationToken);
    }
}
