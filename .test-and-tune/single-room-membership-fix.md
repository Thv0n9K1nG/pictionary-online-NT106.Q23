# Fix: không cho một người thuộc nhiều room

## Vấn đề

Trong logic cũ, Gateway chỉ kiểm tra `sessionId` hợp lệ trước khi xử lý `CreateRoom` hoặc `Join`.

Sau khi GameServer trả response thành công, Gateway mới cập nhật:

- `SessionInfo.RoomCode`
- `ClientConnectionDirectory`
- `RoomDirectory`

Vì không có guard trước request, một người chơi có thể gửi tiếp `CreateRoom` hoặc `Join` để được thêm vào room khác. Trường hợp xấu hơn là hai request tạo/vào phòng đến gần như đồng thời, cả hai đều có thể đi qua trước khi `RoomCode` kịp được set.

## Thay đổi đã thực hiện

### 1. Chặn membership ở Gateway theo `playerId`

File: `Gateway/Managers/SessionDirectory.cs`

Thêm cơ chế kiểm soát room membership:

- `TryBeginRoomMembership(playerId, out currentRoomCode, out error)`
  - Quét các session hiện có của cùng `playerId`.
  - Nếu player đã có `RoomCode`, từ chối create/join room mới.
  - Nếu player đang có một request room operation khác đang chạy, từ chối request song song.
- `EndRoomMembership(playerId)`
  - Gỡ trạng thái pending sau khi request create/join kết thúc.
- `ClearRoom(roomCode)`
  - Clear `RoomCode` của tất cả session thuộc room sau khi game kết thúc.

Mục tiêu là chặn theo người chơi (`playerId`), không chỉ theo một connection/session cụ thể.

### 2. Chặn trước khi Gateway gọi GameServer

File: `Gateway/Handlers/ClientHandler.cs`

Trong:

- `HandleCreateRoomAsync`
- `HandleJoinRoomAsync`

Gateway giờ gọi `TryBeginRoomMembership(...)` trước khi gửi internal request sang GameServer.

Nếu player đã thuộc room khác, Gateway trả `MessageType.Error` với nội dung:

```text
Player is already in room <roomCode>.
```

Nếu player spam hai request create/join cùng lúc, request sau sẽ bị chặn với:

```text
A room operation is already in progress for this player.
```

Sau khi request hoàn tất, Gateway luôn gọi `EndRoomMembership(...)` trong `finally`.

### 3. Dọn mapping room-client theo một room duy nhất

File: `Gateway/Managers/ClientConnectionDirectory.cs`

Thêm reverse map `sessionId -> roomCode`.

Khi gọi `JoinRoom(roomCode, sessionId)`:

- Nếu session từng nằm ở room khác, mapping cũ sẽ bị xóa.
- Mapping mới được ghi vào `_sessionRooms`.

Thêm:

- `RemoveRoom(roomCode)`
  - Xóa toàn bộ session mapping của room.
  - Dùng khi game kết thúc.

### 4. Xóa room khỏi directory khi game kết thúc

File: `Gateway/Managers/RoomDirectory.cs`

Thêm:

- `Remove(roomCode)`
  - Xóa room khỏi `_rooms`.
  - Xóa owner khỏi `_roomOwners`.

### 5. Clear membership sau `GameEnd`

File: `Gateway/Core/GatewayServer.cs`

Trong `HandleServerEventAsync`, sau khi Gateway broadcast `GameEnd` về client:

- `SessionDirectory.ClearRoom(roomCode)`
- `ClientConnectionDirectory.RemoveRoom(roomCode)`
- `RoomDirectory.Remove(roomCode)`

Nhờ vậy người chơi không bị kẹt vĩnh viễn trong room cũ sau khi bấm Back to Lobby.

## Hướng dẫn test

### Test 1: Một client không thể tạo nhiều room

1. Chạy Gateway.
2. Chạy ít nhất một GameServer.
3. Mở Client A và login.
4. Ở Lobby, bấm `Tạo Phòng`.
5. Khi đã vào room, bấm `Tạo Phòng` lần nữa.

Kỳ vọng:

- Request thứ hai bị từ chối.
- Client nhận lỗi dạng `Player is already in room <roomCode>.`
- Gateway không tạo thêm room mới cho player đó.

### Test 2: Một client không thể join room khác khi đã ở room

1. Client A tạo hoặc join Room 1.
2. Tạo Room 2 bằng một client khác.
3. Client A nhập mã Room 2 và bấm vào phòng.

Kỳ vọng:

- Client A không vào được Room 2.
- Gateway trả lỗi `Player is already in room <Room 1>.`
- Player list của Room 2 không có Client A.

### Test 3: Cùng tài khoản login ở hai máy không thể vào hai room khác nhau

1. Client A máy 1 login bằng tài khoản X và vào Room 1.
2. Client B máy 2 login bằng cùng tài khoản X.
3. Client B thử tạo room mới hoặc join Room 2.

Kỳ vọng:

- Client B bị từ chối vì cùng `playerId` đã có room.
- Tài khoản X không thể xuất hiện ở hai room khác nhau.

### Test 4: Spam request create/join song song

1. Login một client.
2. Bấm nhanh create/join nhiều lần hoặc gửi request thủ công gần như đồng thời.

Kỳ vọng:

- Chỉ một request room operation được xử lý.
- Request còn lại nhận lỗi `A room operation is already in progress for this player.`

### Test 5: Sau khi game kết thúc có thể vào room mới

1. Cho ít nhất 2 player chơi đến `GameEnd`.
2. Ở result form, bấm `Back to Lobby`.
3. Một player tạo room mới hoặc join room mới.

Kỳ vọng:

- Player có thể vào room mới.
- Room cũ không còn xuất hiện trong waiting room list.

## Kiểm tra build

Đã chạy:

```powershell
$out = Join-Path $env:TEMP ('pictionary-build-' + [guid]::NewGuid().ToString('N')); dotnet build PictionaryOnline.sln -p:BaseOutputPath=$out
```

Kết quả:

- Build succeeded.
- Chỉ còn warning `NU1701` của package `Siticone.Desktop.UI 2.1.1`, không liên quan đến thay đổi này.

