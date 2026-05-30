# LAN login receive path fix

## Context

During LAN testing, Gateway logged `Login success` for client 2, but client 2 did not appear to process the response, so the `Open Lobby` button stayed unavailable.

I could not reproduce against the second machine IP from this environment, but a source scan found a plausible client-side failure mode: Gateway's receive path is tolerant, while Client's receive path was strict and silent.

## Findings

- `Gateway/Handlers/ClientHandler.cs` sends `LoginSuccess` correctly through `GameMessage.ToJsonLine()`, ending every JSON message with `\n`.
- `scripts/smoke-stage4.ps1` confirmed two raw TLS clients receive `type=14` login success from Gateway.
- `Client/Services/SocketService.cs` previously swallowed JSON parse exceptions and subscriber exceptions without surfacing any status to UI.
- `SocketService` also treated an empty line as a hard disconnect. If a blank frame ever appears in the stream, the client receive loop can stop.
- `Client/UI/LoginForm.cs` only enabled `Open Lobby` inside the message callback, so any silent parse/UI callback failure looked exactly like "Gateway success but client received nothing".

## Fix

Changed `Client/Services/SocketService.cs`:

- Added tolerant message parsing for numeric and string message types, including `14`, `LoginSuccess`, and `LOGIN_SUCCESS`.
- Preserved newline-based framing.
- Blank lines are now ignored instead of closing the connection.
- JSON/connection/subscriber errors are surfaced through `ReceiveError`.
- One failing subscriber no longer prevents other UI handlers from receiving the same message.
- Added `ConnectionClosed` event so UI can show a clear disconnected state.

Changed `Client/UI/LoginForm.cs`:

- Displays receive/parse/connection errors in the status label.
- Enables `Open Lobby` only after `LoginSuccess` is dispatched and `ClientState.SessionId` is populated.
- Uses a safe UI-thread helper for socket callbacks.

## Verification

Ran:

```powershell
dotnet build PictionaryOnline.sln
powershell -ExecutionPolicy Bypass -File scripts\smoke-stage4.ps1 -SkipNaturalExpiry
```

Result:

- Build succeeded with 0 warnings and 0 errors.
- Smoke test passed with `STAGE4_SMOKE_PASS`.
- Both smoke clients received `type=14` login success, then created/joined a room successfully.

## LAN retest checklist

1. Start Gateway on machine 1 and confirm it listens on `0.0.0.0:5000`.
2. Allow inbound TCP port `5000` through Windows Firewall on machine 1.
3. From machine 2, connect Client to machine 1's LAN IP, not `127.0.0.1`.
4. Login client 2 and watch the LoginForm status label:
   - success: `Xin chao, <username>!` and `Open Lobby` enabled;
   - parse/network issue: status label should now show the specific receive error instead of staying silent.
5. If it still fails, capture Gateway console lines around client 2 login and the exact Client status text.

## Commit suggestion

Suggested commit:

```powershell
git add Client\Services\SocketService.cs Client\UI\LoginForm.cs .test-and-tune\lan-client-login-receive-fix.md
git commit -m "fix(client): harden login response receive path"
git push origin develop
```
