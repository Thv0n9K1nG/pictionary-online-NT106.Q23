# Pictionary Online

Đồ án môn Lập trình mạng căn bản.

## Giảng viên hướng đẫn

- Ths. Đỗ Thị Hương Lan

## Thực hiện bởi nhóm 11 gồm:

- Hồ Ngọc Vương Thương - 24521749

- Phạm Trần Anh Tuấn - 24521939

- Võ Đình Hoàng Tiến - 24521783

- Hà Võ Đức Thiện - 24521658

## Lớp

NT106.Q23.ANTT

---

## Architecture

Gateway-centric multi-server architecture:

- Client chỉ kết nối tới Gateway qua TLS.
- Gateway là Load Balancer và message relay.
- Nhiều Game Server đều active.
- Mỗi room có một authoritative owner tại một thời điểm.
- Gateway dùng RoomDirectory để route message tới đúng Game Server owner.
- Game Server gửi checkpoint nhẹ về Gateway để hỗ trợ reconnect/recovery.
- Database không nằm trên hot path realtime.

## Projects

- `Shared`: protocol, models, enums dùng chung.
- `Gateway`: TLS entrypoint, auth, session, load balancing, room routing, persistence.
- `GameServer`: room owner, gameplay runtime state, game engine, checkpoint.
- `Client`: WinForms UI, SocketService, DrawingCanvas.

## Build

```powershell
dotnet restore
dotnet build -c Debug

## Run baseline
dotnet run --project Gateway -- 5000
dotnet run --project GameServer -- gs1 127.0.0.1 6000
dotnet run --project Client

Baseline hiện tại dựng skeleton compile được. Socket accept loop, Gateway relay, GameServer registration và gameplay implementation sẽ được phát triển theo timeline tuần 10-15.
```
