# Tune: chuyển room, logout lobby và chuyển host khi host rời phòng

## Vấn đề sau bản fix trước

Bản fix trước chặn player đã có `RoomCode` tạo hoặc join room mới. Điều này đúng ở mặt chống một tài khoản thuộc nhiều room, nhưng bị quá cứng:

- User lỡ tạo room thì bị khóa trong room đó.
- User không thể chuyển sang room khác.
- Nếu host quay lại login/logout thì room cũ vẫn giữ state nếu không có cơ chế leave/delete/transfer.

## Hướng xử lý mới

Không còn xử lý theo kiểu "đã có room thì cấm". Thay vào đó:

1. Khi user tạo room mới hoặc join room khác, Gateway tự rời room cũ trước.
2. Nếu room cũ chỉ còn user đó, GameServer xóa room.
3. Nếu room cũ còn người khác, GameServer remove user đó và chuyển host cho một người còn lại nếu user rời là host.
4. Khi user bấm Logout ở Lobby, Gateway cũng chạy cùng cơ chế leave room.

## File đã chỉnh

### Shared/Enums/MessageType.cs

Thêm:

- `Logout = 34`
- `LogoutSuccess = 35`

Client dùng `Logout` để báo Gateway cleanup session/room trước khi quay lại login.

### Shared/Protocol/InternalMessageType.cs

Thêm:

- `LeaveRoom`

Gateway dùng message nội bộ này để yêu cầu GameServer remove player khỏi room.

### GameServer/Rooms/GameRoom.cs

Thêm `RemovePlayer(playerId)`:

- Xóa player khỏi danh sách room.
- Xóa session mapping, guess/draw score tracking liên quan player đó.
- Nếu player rời là host và room còn người khác, player đầu tiên còn lại được set `IsHost = true`.
- Nếu không còn player nào, room được xem là deleted.

### GameServer/Managers/RoomManager.cs

Thêm `LeaveRoom(roomCode, playerId)`:

- Gọi `GameRoom.RemovePlayer`.
- Nếu room rỗng thì remove khỏi `_rooms`.
- Trả về `LeaveRoomResult` gồm `RoomDeleted`, `RoomInfo`, `Players`.

### GameServer/Handlers/GatewayHandler.cs

Thêm handler cho `InternalMessageType.LeaveRoom`:

- Đọc `roomCode`, `playerId`.
- Gọi `RoomManager.LeaveRoom`.
- Trả response về Gateway qua `ServerEvent`.
- Nếu room còn tồn tại thì gửi checkpoint mới.

### Gateway/Managers/SessionDirectory.cs

Đổi cơ chế khóa:

- Từ `TryBeginRoomMembership` cấm player đã có room.
- Sang `TryBeginRoomOperation` chỉ chống spam/race nhiều thao tác room song song.

Thêm:

- `GetRoomMembershipsForPlayer(playerId)`: lấy tất cả session đang có room của cùng player.
- `SetRoom(sessionId, roomCode)`: set/clear room cho một session.

### Gateway/Managers/ClientConnectionDirectory.cs

Thêm:

- `LeaveRoom(roomCode, sessionId)`: gỡ một session khỏi room nhưng vẫn giữ connection online.

### Gateway/Handlers/ClientHandler.cs

Thay đổi chính:

- `HandleCreateRoomAsync`:
  - Lock room operation theo `playerId`.
  - Gọi `LeaveExistingRoomsForPlayerAsync(...)` trước khi tạo room mới.
  - Tạo room mới như bình thường.

- `HandleJoinRoomAsync`:
  - Lock room operation theo `playerId`.
  - Leave các room cũ, trừ room target nếu user đang ở chính room đó.
  - Join room target.
  - Không tăng `ActivePlayers` nếu user join lại chính room đang đứng.

- `HandleLogoutAsync`:
  - Leave tất cả room hiện tại của player.
  - Remove session khỏi `SessionDirectory`.
  - Remove connection mapping khỏi `ClientConnectionDirectory`.
  - Trả `LogoutSuccess`.

- `LeaveExistingRoomsForPlayerAsync`:
  - Gom membership theo room để tránh một player nhiều session làm leave nhiều lần cùng room.
  - Gửi internal `LeaveRoom` tới GameServer owner.

- `ApplyLeaveRoomResponseAsync`:
  - Nếu room deleted: xóa `RoomDirectory`, clear session room, remove room mapping, giảm load room/player.
  - Nếu room còn người: update `RoomDirectory`, clear session rời phòng, broadcast `PlayerList` mới cho người còn lại.

### Client/Services/GameMessageFactory.cs

Thêm:

- `Logout(sessionId)`

### Client/State/ClientState.cs

Thêm:

- `ClearSession()`

Clear user/session/room/player list khi client logout quay về login.

### Client/UI/LobbyForm.cs

Thêm nút `Logout` ở thanh công cụ lobby.

Khi bấm:

1. Gửi `GameMessageFactory.Logout(_state.SessionId)`.
2. Nếu gửi thành công, gọi `_state.ClearSession()`.
3. Đóng `LobbyForm`, để `LoginForm` hiện lại theo flow hiện có.

## Kịch bản test

### 1. Tạo room rồi chuyển sang room khác

1. Client A login.
2. Client A tạo Room A.
3. Client B tạo Room B.
4. Client A join Room B.

Kỳ vọng:

- Client A rời Room A.
- Nếu Room A chỉ có Client A, Room A bị xóa khỏi Gateway/GameServer.
- Client A vào Room B thành công.

### 2. Host rời room khi còn người khác

1. Client A tạo room.
2. Client B join room.
3. Client A join room khác hoặc bấm Logout.

Kỳ vọng:

- Room cũ không bị xóa vì còn Client B.
- Client B được chuyển thành host.
- Client B nhận `PlayerList` mới.

### 3. Host logout khi một mình trong room

1. Client A tạo room.
2. Client A bấm `Logout`.

Kỳ vọng:

- Gateway gửi `LeaveRoom` sang GameServer.
- GameServer xóa room vì không còn player.
- Client A quay về LoginForm.
- Room cũ không xuất hiện khi client khác refresh room list.

### 4. Non-host logout

1. Client A tạo room.
2. Client B join room.
3. Client B bấm `Logout`.

Kỳ vọng:

- Client B rời room và quay về LoginForm.
- Client A vẫn là host.
- PlayerList của Client A chỉ còn Client A.

### 5. Spam create/join

1. Client A login.
2. Bấm nhanh create/join nhiều lần.

Kỳ vọng:

- Gateway chỉ xử lý một room operation tại một thời điểm theo `playerId`.
- Không còn tình trạng một player nằm trong nhiều room.

## Kiểm tra build

Đã chạy:

```powershell
$out = Join-Path $env:TEMP ('pictionary-build-' + [guid]::NewGuid().ToString('N')); dotnet build PictionaryOnline.sln -p:BaseOutputPath=$out
```

Kết quả:

- Build succeeded.
- Chỉ còn warning `NU1701` của `Siticone.Desktop.UI 2.1.1`, không liên quan thay đổi này.

