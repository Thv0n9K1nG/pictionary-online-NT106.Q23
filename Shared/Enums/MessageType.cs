namespace Shared.Enums;

public enum MessageType
{
    Unknown = 0,

    // Client -> Gateway
    Register = 1,
    Login = 2,
    CreateRoom = 3,
    Join = 4,
    GetRoomList = 5,
    Ready = 6,
    SelectWord = 7,
    Draw = 8,
    Guess = 9,
    Chat = 10,
    Reconnect = 11,

    // Gateway -> Client
    RegisterSuccess = 12,
    RegisterFailed = 13,
    LoginSuccess = 14,
    LoginFailed = 15,
    RoomList = 16,
    RoomJoined = 17,
    PlayerList = 18,
    GameStart = 19,
    WordOptions = 20,
    DrawData = 21,
    CorrectGuess = 22,
    TimerUpdate = 23,
    RoundEnd = 24,
    GameEnd = 25,
    Hint = 26,
    PlayerLeft = 27,
    RoomRecovered = 28,
    Error = 29,

    // Stage 6 - C: history and stats requests keep old numeric protocol stable.
    GetMatchHistory = 30,
    GetPlayerStats = 31,
    MatchHistoryResult = 32,
    PlayerStatsResult = 33,
    Logout = 34,
    LogoutSuccess = 35,
    LeaveRoom = 36,
    LeaveRoomSuccess = 37
}
