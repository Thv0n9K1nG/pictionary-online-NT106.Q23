using System.Collections.Concurrent;
using Shared.Models;

namespace Gateway.Managers;

public sealed class SessionDirectory
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    public void Set(SessionInfo session)
    {
        _sessions[session.SessionId] = session;
    }

    public bool TryGet(string sessionId, out SessionInfo? session)
    {
        return _sessions.TryGetValue(sessionId, out session);
    }

    public bool Remove(string sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }
}
