using System.Collections.Concurrent;
using Gateway.Handlers;

namespace Gateway.Managers;

public sealed class ClientConnectionDirectory
{
    private readonly ConcurrentDictionary<string, ClientHandler> _sessions = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _roomSessions = new();

    public void RegisterSession(string sessionId, ClientHandler handler)
    {
        _sessions[sessionId] = handler;
    }

    public void JoinRoom(string roomCode, string sessionId)
    {
        var sessions = _roomSessions.GetOrAdd(roomCode, _ => new ConcurrentDictionary<string, byte>());
        sessions[sessionId] = 0;
    }

    public IReadOnlyList<ClientHandler> GetRoomClients(string roomCode)
    {
        if (!_roomSessions.TryGetValue(roomCode, out var sessions))
        {
            return [];
        }

        return sessions.Keys
            .Select(sessionId => _sessions.TryGetValue(sessionId, out var handler) ? handler : null)
            .OfType<ClientHandler>()
            .ToList();
    }

    public IReadOnlyList<ClientHandler> GetClients(IEnumerable<string> sessionIds)
    {
        return sessionIds
            .Select(sessionId => _sessions.TryGetValue(sessionId, out var handler) ? handler : null)
            .OfType<ClientHandler>()
            .ToList();
    }

    public void RemoveSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);

        foreach (var sessions in _roomSessions.Values)
        {
            sessions.TryRemove(sessionId, out _);
        }
    }
}
