using System.Collections.Concurrent;
using Shared.Models;

namespace Gateway.Managers;

public sealed class SessionDirectory
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private readonly HashSet<string> _playersWithPendingRoomOperation = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();

    public void Set(SessionInfo session)
    {
        lock (_syncRoot)
        {
            _sessions[session.SessionId] = session;
        }
    }

    public bool TryGet(string sessionId, out SessionInfo? session)
    {
        lock (_syncRoot)
        {
            return _sessions.TryGetValue(sessionId, out session);
        }
    }

    public bool Remove(string sessionId)
    {
        lock (_syncRoot)
        {
            return _sessions.TryRemove(sessionId, out _);
        }
    }

    public bool TryBeginRoomOperation(string playerId, out string error)
    {
        lock (_syncRoot)
        {
            if (!_playersWithPendingRoomOperation.Add(playerId))
            {
                error = "A room operation is already in progress for this player.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    public void EndRoomOperation(string playerId)
    {
        lock (_syncRoot)
        {
            _playersWithPendingRoomOperation.Remove(playerId);
        }
    }

    public IReadOnlyList<SessionInfo> GetRoomMembershipsForPlayer(string playerId)
    {
        lock (_syncRoot)
        {
            return _sessions.Values
                .Where(session =>
                    string.Equals(session.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(session.RoomCode))
                .ToList();
        }
    }

    public void SetRoom(string sessionId, string? roomCode)
    {
        lock (_syncRoot)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                _sessions[sessionId] = session with { RoomCode = roomCode };
            }
        }
    }

    public void ClearRoom(string roomCode)
    {
        lock (_syncRoot)
        {
            foreach (var session in _sessions.Values.Where(session =>
                         string.Equals(session.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _sessions[session.SessionId] = session with { RoomCode = null };
            }
        }
    }
}
