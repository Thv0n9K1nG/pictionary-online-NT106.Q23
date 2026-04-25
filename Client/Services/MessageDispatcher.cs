using Client.State;
using Shared.Enums;
using Shared.Models;

namespace Client.Services;

public sealed class MessageDispatcher
{
    private readonly ClientState _state;

    public MessageDispatcher(ClientState state)
    {
        _state = state;
    }

    public void Dispatch(GameMessage message)
    {
        switch (message.Type)
        {
            case MessageType.LoginSuccess:
            case MessageType.RoomJoined:
            case MessageType.PlayerList:
            case MessageType.DrawData:
            case MessageType.Error:
                // TODO: Update state and UI through UiThreadDispatcher.
                break;

            default:
                break;
        }
    }
}
