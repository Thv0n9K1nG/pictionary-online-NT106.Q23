<p align="center">
  <img src="Client/logo.png" alt="Pictionary Online logo" width="180" />
</p>

# Pictionary Online

Pictionary Online is a real-time multiplayer drawing and guessing game built with C# and .NET 8. The system follows a gateway-centric multi-server architecture where clients connect only to the Gateway, while gameplay rooms are distributed across active Game Server nodes.

## Highlights

- Real-time drawing, guessing, chat, scoring, timer, and round flow.
- Gateway-only client connection model with TLS on the client-facing socket.
- Active Game Server cluster with room-level load balancing.
- Room ownership tracking through `RoomDirectory`.
- Game Server heartbeat tracking through `NodeRegistry`.
- Lightweight room checkpointing for reconnect and recovery scenarios.
- SQLite persistence for users, sessions, match history, and player stats.
- WinForms desktop client with lobby, room list, canvas, scoreboard, and result screens.

## Architecture

```text
Client WinForms
    |
    | TCP over TLS
    v
Gateway / Load Balancer
    |
    | Internal TCP
    v
Active Game Server Cluster
    |
    v
SQLite Persistence
```

Core principles:

- Clients never connect directly to Game Servers.
- Gateway is the public entry point and owns auth, session management, room routing, load balancing, persistence, and recovery coordination.
- Game Servers are active runtime nodes. Each room has exactly one authoritative owner at a time.
- Gateway routes room messages to the current owner server.
- New rooms are assigned to the healthiest and least-loaded available Game Server.

## Projects

| Project | Purpose |
| --- | --- |
| `Client` | WinForms desktop application, socket client, reconnect flow, lobby, gameplay UI, and drawing canvas. |
| `Gateway` | Public server entry point, TLS listener, authentication, session management, load balancing, room directory, routing, persistence, and recovery. |
| `GameServer` | Active gameplay node, room state, game engine, timer, scoring, draw/guess handling, and checkpoint reporting. |
| `Shared` | Shared protocol models, enums, message contracts, room info, heartbeat info, snapshots, and match result DTOs. |

## Requirements

- .NET SDK 8.0 or later
- Windows for running the WinForms client
- SQLite support through `Microsoft.Data.Sqlite`

## Build

```powershell
dotnet restore
dotnet build PictionaryOnline.sln
```

If Gateway, GameServer, or Client processes are already running and locking build output files, build to a temporary output path:

```powershell
$out = Join-Path $env:TEMP ('pictionary-build-' + [guid]::NewGuid().ToString('N'))
dotnet build PictionaryOnline.sln -p:BaseOutputPath=$out
```

## Run Locally

Start the Gateway:

```powershell
dotnet run --project Gateway/Gateway.csproj -- 5000 6000
```

Start two Game Server nodes:

```powershell
dotnet run --project GameServer/GameServer.csproj -- gs-01 127.0.0.1 6000
dotnet run --project GameServer/GameServer.csproj -- gs-02 127.0.0.1 6000
```

Start the Client:

```powershell
dotnet run --project Client/Client.csproj
```

Default ports:

| Port | Used by | Description |
| --- | --- | --- |
| `5000` | Client -> Gateway | TLS client-facing socket. |
| `6000` | GameServer -> Gateway | Internal Game Server TCP socket. |

## Load Balancing Demo

The load balancer assigns new rooms to Game Server nodes based on current load:

```text
ActiveRooms * 2 + ActivePlayers
```

Demo flow:

1. Start Gateway.
2. Start `gs-01` and `gs-02`.
3. Confirm Gateway logs show both nodes registered and sending heartbeats.
4. Open a client, log in, and create the first room.
5. Open another client with a different account and create a second room.
6. Observe the Game Server logs. Room creation should be distributed between `gs-01` and `gs-02` as load changes.

Useful log lines:

```text
[Gateway] GameServer registered: gs-01
[Gateway] GameServer registered: gs-02
[Gateway] Heartbeat from gs-01: rooms=0, players=0, canAccept=True
[Gateway] Heartbeat from gs-02: rooms=0, players=0, canAccept=True
[GameServer:gs-01] Created room ...
[GameServer:gs-02] Created room ...
```

## Azure Deployment Notes

Recommended network layout:

- Expose Gateway client port `5000` publicly for clients.
- Keep Gateway internal Game Server port `6000` private whenever possible.
- Run `gs-01` and `gs-02` against the Gateway private IP or internal hostname when they are in the same Azure virtual network.
- Clients should connect only to the Gateway public IP or domain.

Example Game Server startup against a Gateway private address:

```powershell
dotnet run --project GameServer/GameServer.csproj -- gs-01 <gateway-private-ip> 6000
dotnet run --project GameServer/GameServer.csproj -- gs-02 <gateway-private-ip> 6000
```

## Repository Structure

```text
.
+-- Client/        # WinForms client application
+-- Gateway/       # Gateway, load balancer, routing, auth, persistence
+-- GameServer/    # Active gameplay server node
+-- Shared/        # Shared protocol and data contracts
+-- context/       # Project analysis and design documents
+-- scripts/       # Utility scripts
+-- PictionaryOnline.sln
```

## Development Notes

- Protocol messages use JSON lines over TCP.
- Client-facing Gateway communication uses TLS.
- Internal Gateway-to-GameServer communication uses TCP.
- Realtime gameplay state lives in Game Server memory.
- Persistence is intentionally kept off the realtime drawing hot path.
- Checkpoints are lightweight snapshots used for reconnect and recovery demonstrations.
