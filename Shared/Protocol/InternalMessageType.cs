namespace Shared.Protocol;

public enum InternalMessageType
{
    Unknown = 0,
    NodeRegister,
    NodeHeartbeat,
    CreateRoomOnNode,
    ForwardClientMessage,
    ServerEvent,
    RoomCheckpoint,
    RestoreRoomFromCheckpoint,
    RoomRestored,
    MatchResult,
    LeaveRoom
}
