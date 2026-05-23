# Stage 4 Change Summary

## Detailed Summary By File

### GameServer/Core/GameServerNode.cs

- Added a send lock around internal Gateway writes.
- Prevents heartbeat, room events, timer expiry events, and other internal messages from writing to the same network stream at the same time.
- This makes the GameServer -> Gateway protocol safer during active gameplay rounds.

### GameServer/Engine/GameEngine.cs

- Implemented core gameplay helpers for Stage 4.
- Added 60-second round duration constant.
- Added word option loading through Gemini with WordBank fallback.
- Added masked word generation for guessers.
- Added case-insensitive, trimmed guess comparison.
- Added score calculation helpers for guessers and drawer.

### GameServer/Handlers/GatewayHandler.cs

- Added handling for forwarded gameplay messages from Gateway.
- Supports `Ready`, `SelectWord`, and `Guess` flows on the GameServer.
- Sends `GameStart`, `WordOptions`, `Hint`, `TimerUpdate`, `CorrectGuess`, `PlayerList`, `RoundEnd`, and `GameEnd` events.
- Sends word options only to the drawer and hints only to guessers.
- Starts a 60-second timer after word selection and ends the round automatically when time expires.

### GameServer/Managers/RoomManager.cs

- Expanded room orchestration for Stage 4 gameplay.
- Added methods for ready/start round, word selection, guessing, and round expiration.
- Converts gameplay room states into `RoomInfo` status for Gateway room listing/routing.
- Centralizes calls into `GameEngine` so room state and scoring stay consistent.

### GameServer/Rooms/GameRoom.cs

- Reworked room state model for playable rounds.
- Tracks players, player sessions, current drawer, current word, word options, round timestamps, correct guessers, and completed rounds.
- Enforces host-only ready, minimum 2 players, drawer-only word selection, and non-drawer guessing.
- Handles score updates, drawer rotation, round end, game over, and `MatchResult` creation.

### Gateway/Core/GatewayServer.cs

- Added JSON enum handling for server-originated game events.
- Added support for targeted room events through `targetSessionIds`.
- Allows GameServer to send private events such as `WordOptions` only to drawer and `Hint` only to guessers.

### Gateway/Handlers/ClientHandler.cs

- Routed Stage 4 gameplay client messages through `ProxyRouter`.
- Added handling for `Ready`, `SelectWord`, `Guess`, and `Chat` message types.
- Validates room code and session before forwarding gameplay actions to the owning GameServer.

### Gateway/Managers/ClientConnectionDirectory.cs

- Added lookup of multiple client handlers by session IDs.
- Enables targeted server events for selected clients in a room.

### Gateway/Services/ProxyRouter.cs

- Extended proxy routing to forward gameplay messages to the room owner GameServer.
- Validates session and room owner before forwarding.
- Uses internal request/response flow so Gateway can surface GameServer rejection errors back to clients.

### global.json

- Restored SDK requirement to installed .NET 8 SDK.
- Current value is `8.0.419` with `rollForward: latestFeature`.
- Fixes `dotnet build` failing because the repo previously requested unavailable SDK `10.0.201`.

### scripts/smoke-stage4.ps1

- Added reusable Stage 4 smoke test script.
- Starts Gateway and one GameServer on temporary ports.
- Connects two TLS clients, registers/logs in, creates and joins a room, starts gameplay, verifies word options, hint, timer, scoring, round end, drawer rotation, natural 60-second expiry, and game end.
- Supports `-SkipNaturalExpiry` for a faster test that skips the 60-second wait.

### pictionary.db

- Local SQLite database changed because smoke tests/register/login flows wrote test users and room data.
- This is a test artifact, not a Stage 4 code change.
- Recommended: do not include this file in the PR unless the team intentionally versions database state.

## One-Line PR Description Notes

- `GameServer/Core/GameServerNode.cs`: Serialize internal writes to Gateway so heartbeat and gameplay events cannot interleave on the same stream.
- `GameServer/Engine/GameEngine.cs`: Add Stage 4 gameplay helpers for 60-second rounds, word options, masked hints, guess matching, and scoring.
- `GameServer/Handlers/GatewayHandler.cs`: Handle Ready/SelectWord/Guess and emit targeted gameplay events, round timer expiry, round end, and game end.
- `GameServer/Managers/RoomManager.cs`: Add room-level orchestration for starting rounds, selecting words, applying guesses, and expiring rounds.
- `GameServer/Rooms/GameRoom.cs`: Implement gameplay room state, drawer rotation, scoring, round transitions, and match result creation.
- `Gateway/Core/GatewayServer.cs`: Support targeted room events and string enum deserialization for GameServer-originated messages.
- `Gateway/Handlers/ClientHandler.cs`: Forward Stage 4 gameplay messages from clients to the owning GameServer through ProxyRouter.
- `Gateway/Managers/ClientConnectionDirectory.cs`: Add session-based multi-client lookup for private gameplay events.
- `Gateway/Services/ProxyRouter.cs`: Route gameplay actions to the room owner GameServer with session and room ownership validation.
- `global.json`: Pin the repo back to .NET SDK 8.0.419 with feature roll-forward.
- `scripts/smoke-stage4.ps1`: Add an end-to-end Stage 4 smoke test covering two clients, scoring, timer expiry, and game end.
- `pictionary.db`: Local test database changed during smoke tests; exclude from PR unless database state is intentionally committed.

## Verification

```powershell
dotnet build
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-stage4.ps1
```

Latest verification result:

```text
dotnet build: succeeded with 0 warnings and 0 errors
smoke-stage4.ps1: STAGE4_SMOKE_PASS
```
