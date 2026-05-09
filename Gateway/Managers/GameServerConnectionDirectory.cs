using System.Collections.Concurrent;
using Gateway.Handlers;

namespace Gateway.Managers;

public sealed class GameServerConnectionDirectory
{
    private readonly ConcurrentDictionary<string, GameServerHandler> _handlers = new();

    public void Register(string serverId, GameServerHandler handler)
    {
        _handlers[serverId] = handler;
    }

    public bool TryGet(string serverId, out GameServerHandler? handler)
    {
        return _handlers.TryGetValue(serverId, out handler);
    }

    public IReadOnlyList<GameServerHandler> GetAll()
    {
        return _handlers.Values
            .Where(handler => !string.IsNullOrWhiteSpace(handler.ServerId))
            .ToList();
    }

    public void Remove(string serverId)
    {
        _handlers.TryRemove(serverId, out _);
    }
}
