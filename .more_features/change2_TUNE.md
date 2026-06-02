# Change 2 Tune - Auto Lobby, Manual Game Start, Back to Lobby

## Muc tieu

Hoan thien Change 2 va dieu chinh pham vi Change 3 theo quyet dinh moi:

- Login thanh cong tu mo Lobby.
- Khong con nut `Open Lobby` tren LoginForm.
- Khong mo trung Lobby khi nhan duplicate/late `LoginSuccess`.
- Lobby tu refresh room list khi mo.
- Khong auto mo GameForm sau create/join room.
- User van bam nut `Bat dau Game` trong Lobby moi vao GameForm.
- Chong mo GameForm nhieu lan.
- ResultForm co nut `Back to Lobby` o ket qua chung cuoc.

## File da chinh sua

### `Client/UI/LoginForm.cs`

Trang thai hien tai:

- `Open Lobby` da duoc bo khoi normal flow.
- `LoginSuccess` goi `OpenLobbyOnce()`.
- `_lobbyOpened` chan mo nhieu LobbyForm neu Gateway gui duplicate/late message.
- Login sai/Register fail khong chuyen man hinh, chi hien MessageBox.

Khong thay doi them trong lan tune nay vi flow auto Lobby da co san.

### `Client/UI/LobbyForm.cs`

Da them:

- `_gameOpened` de chan mo nhieu GameForm cung luc.
- `_roomListRequestedOnShown` de Lobby chi auto refresh room list mot lan khi mo.
- `Shown += RefreshRoomListOnFirstShowAsync`.
- Nut refresh dung chung `RefreshRoomListAsync()`.
- `OpenGame()` co guard:
  - khong co `RoomCode` thi khong vao game;
  - dang mo game roi thi return;
  - disable nut khi GameForm dang mo;
  - khi GameForm dong thi reset guard va hien lai Lobby.

Quan trong: `RoomJoined` chi update status va enable nut `Bat dau Game`, khong auto open GameForm.

### `Client/UI/GameForm.cs`

Da them:

- `_eventsRegistered` de tranh register event nhieu lan trong cung instance.
- `_eventsUnregistered` va `UnregisterEvents()` de go cac event co ten khi GameForm dong.
- Guard no-op cho callback cu neu form da dong/disposed.
- `_roundResultDialogOpen` de tranh mo nhieu ResultForm cho round result.
- `_gameResultDialogOpen` de tranh mo nhieu ResultForm cho final game result.
- Final result goi:

```csharp
form.EnableBackToLobby();
```

Neu user bam Back to Lobby, GameForm se `Close()`, va LobbyForm dang `ShowDialog()` se hien lai.

### `Client/UI/ResultForm.cs`

Da them:

- `BackToLobbyRequested`.
- `EnableBackToLobby()`.
- Nut `Back to Lobby`, mac dinh hidden.
- Chi final game result bat nut nay; round result khong bat.

## Hanh vi sau tune

### Login flow

```text
Client connected -> Login dung -> Splash -> Lobby
```

Khong can bam `Open Lobby`.

### Lobby flow

```text
Lobby mo -> tu gui GET_ROOM_LIST mot lan
Create/Join room -> RoomJoined -> enable Bat dau Game
User bam Bat dau Game -> GameForm mo
```

Khong auto vao GameForm sau create/join.

### Result flow

```text
Round end -> Result round -> dong dialog -> tiep tuc game/ready
Game end -> Result final -> Back to Lobby -> GameForm dong -> Lobby hien lai
```

## Huong dan test

### Test 1 - Login thanh cong vao Lobby

1. Chay Gateway va GameServer.
2. Mo Client.
3. Login dung.
4. Xac nhan Lobby tu mo.
5. Xac nhan khong co nut `Open Lobby`.
6. Neu Gateway gui duplicate `LoginSuccess`, khong mo them Lobby thu hai.

### Test 2 - Lobby tu refresh room list

1. Login vao Lobby.
2. Quan sat room list tu refresh mot lan.
3. Bam `Lam moi` de verify refresh van hoat dong.

### Test 3 - Join/Create khong tu mo GameForm

1. Tao phong hoac join phong.
2. Xac nhan van o Lobby.
3. Xac nhan nut `Bat dau Game` enabled.
4. Chi khi bam `Bat dau Game`, GameForm moi mo.

### Test 4 - Chong mo GameForm nhieu lan

1. Sau khi vao phong, bam `Bat dau Game` nhieu lan nhanh.
2. Xac nhan chi co mot GameForm.
3. Dong GameForm.
4. Xac nhan Lobby hien lai va co the bam `Bat dau Game` lan nua neu van con room context.

### Test 5 - Back to Lobby sau game end

1. Choi den `GameEnd`.
2. ResultForm chung cuoc hien nut `Back to Lobby`.
3. Bam `Back to Lobby`.
4. Xac nhan GameForm dong va Lobby hien lai.
5. Round result khong hien nut Back to Lobby.

## Verification da chay

Da chay full solution build voi output tam rieng:

```powershell
$out = Join-Path $env:TEMP ('pictionary-build-' + [guid]::NewGuid().ToString('N')); dotnet build PictionaryOnline.sln -p:BaseOutputPath=$out
```

Ket qua:

```text
Build succeeded.
0 errors.
```

Warning con lai:

```text
NU1701 Siticone.Desktop.UI restored using .NETFramework target instead of net8.0-windows
```

Day la warning package co san, khong phai loi tu change nay.

## Luu y

- Chua implement server-side Leave/Quit trong change nay.
- `Back to Lobby` hien tai la UX client sau `GameEnd`; no khong gui message LeaveRoom vi game da ket thuc.
- Neu sau nay them Leave/Quit that su, can lam tiep o protocol/Gateway/GameServer.
- `.more_features/` dang bi `.gitignore` ignore, neu commit file nay can dung `git add -f`.

## Goi y commit

```powershell
git add Client\UI\LoginForm.cs Client\UI\LobbyForm.cs Client\UI\GameForm.cs Client\UI\ResultForm.cs
git add -f .more_features\change2_TUNE.md
git commit -m "feat(client): finalize lobby navigation and result return flow"
```
