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

    public static GameMessage Ready(string roomCode, string sessionId)
    {
        return new GameMessage
        {
            Type = MessageType.Ready,
            Payload = new { roomCode, sessionId }
        };
    }

    public static GameMessage SelectWord(string roomCode, string word, string sessionId)
    {
        return new GameMessage
        {
            Type = MessageType.SelectWord,
            Payload = new { roomCode, word, sessionId }
        };
    }

    public static GameMessage Guess(string roomCode, string guess, string sessionId)
    {
        return new GameMessage
        {
            Type = MessageType.Guess,
            Payload = new { roomCode, guess, sessionId }
        };
    }

    public static GameMessage Chat(string roomCode, string text, string sessionId)
    {
        return new GameMessage
        {
            Type = MessageType.Chat,
            Payload = new { roomCode, text, sessionId }
        };
    }
}
