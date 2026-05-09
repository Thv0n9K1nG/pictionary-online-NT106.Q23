using Shared.Enums;
using Shared.Models;

namespace Client.Services;

public static class GameMessageFactory
{
    public static GameMessage Login(string username, string password)
    {
        return new GameMessage
        {
            Type = MessageType.Login,
            Payload = new { username, password }
        };
    }

    public static GameMessage Register(string username, string password)
    {
        return new GameMessage
        {
            Type = MessageType.Register,
            Payload = new { username, password }
        };
    }

    public static GameMessage CreateRoom(string playerName, string sessionId)
    {
        return new GameMessage
        {
            Type = MessageType.CreateRoom,
            Payload = new { playerName, sessionId }
        };
    }

    public static GameMessage JoinRoom(string roomCode, string playerName, string sessionId)
    {
        return new GameMessage
        {
            Type = MessageType.Join,
            Payload = new { roomCode, playerName, sessionId }
        };
    }

    public static GameMessage GetRoomList()
    {
        return new GameMessage
        {
            Type = MessageType.GetRoomList,
            Payload = new { }
        };
    }

    public static GameMessage Draw(string roomCode, DrawPayload payload, string sessionId)
    {
        return new GameMessage
        {
            Type = MessageType.Draw,
            Payload = new { roomCode, drawPayload = payload, sessionId }
        };
    }
}
