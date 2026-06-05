using System.Collections.Concurrent;
using Gateway.Handlers;

namespace Gateway.Managers;

public sealed class ClientConnectionDirectory
{
    private readonly ConcurrentDictionary<string, ClientHandler> _sessions = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _roomSessions = new();
    private readonly ConcurrentDictionary<string, string> _sessionRooms = new();

    public void RegisterSession(string sessionId, ClientHandler handler)
    {
        _sessions[sessionId] = handler;
    }

    public void JoinRoom(string roomCode, string sessionId)
    {
        if (_sessionRooms.TryGetValue(sessionId, out var previousRoomCode) &&
            !string.Equals(previousRoomCode, roomCode, StringComparison.OrdinalIgnoreCase) &&
            _roomSessions.TryGetValue(previousRoomCode, out var previousSessions))
        {
            previousSessions.TryRemove(sessionId, out _);
        }

        var sessions = _roomSessions.GetOrAdd(roomCode, _ => new ConcurrentDictionary<string, byte>());
        sessions[sessionId] = 0;
        _sessionRooms[sessionId] = roomCode;
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

        if (_sessionRooms.TryRemove(sessionId, out var roomCode) &&
            _roomSessions.TryGetValue(roomCode, out var roomSessions))
        {
            roomSessions.TryRemove(sessionId, out _);
        }

        foreach (var sessions in _roomSessions.Values)
        {
            sessions.TryRemove(sessionId, out _);
        }
    }

    public void LeaveRoom(string roomCode, string sessionId)
    {
        if (_roomSessions.TryGetValue(roomCode, out var sessions))
        {
            sessions.TryRemove(sessionId, out _);
        }

        if (_sessionRooms.TryGetValue(sessionId, out var currentRoomCode) &&
            string.Equals(currentRoomCode, roomCode, StringComparison.OrdinalIgnoreCase))
        {
            _sessionRooms.TryRemove(sessionId, out _);
        }
    }

    public void RemoveRoom(string roomCode)
    {
        if (!_roomSessions.TryRemove(roomCode, out var sessions))
        {
            return;
        }

        foreach (var sessionId in sessions.Keys)
        {
            _sessionRooms.TryRemove(sessionId, out _);
        }
    }
}
