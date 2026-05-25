namespace Shared.Enums;

public enum MessageType
{
    Unknown = 0,

    // Client -> Gateway
    Register,
    Login,
    CreateRoom,
    Join,
    GetRoomList,
    Ready,
    SelectWord,
    Draw,
    Guess,
    Chat,
    Reconnect,
    GetMatchHistory,
    GetPlayerStats,
    // Gateway -> Client
    RegisterSuccess,
    RegisterFailed,
    LoginSuccess,
    LoginFailed,
    RoomList,
    RoomJoined,
    PlayerList,
    GameStart,
    WordOptions,
    DrawData,
    CorrectGuess,
    TimerUpdate,
    RoundEnd,
    GameEnd,
    Hint,
    PlayerLeft,
    RoomRecovered,
    Error,
    MatchHistoryResult,
    PlayerStatsResult
}
