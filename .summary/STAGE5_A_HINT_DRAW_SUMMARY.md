# Stage 5 A + Sync Hint Summary

## Ket luan kiem tra

- Stage 4 gameplay core da pass smoke end-to-end sau khi sua timer/hint loop.
- Stage 5 phan B tren client da co day du nen tang: local draw, remote draw, toolbar mau/co co/tay/clear, canvas read-only theo `IsDrawer`.
- Stage 5 phan A backend da duoc hoan thien va test: Gateway forward `DRAW`, GameServer validate quyen ve/state/room, roi broadcast `DRAW_DATA` ve guessers.
- Neu cham that chat theo toan bo F-ID Stage 4, F-47 host transfer chua thay co handler leave/disconnect rieng trong code hien tai.

## Cac thay doi da lam

### GameServer/Engine/GameEngine.cs

- Them cau hinh `HintRevealIntervalSeconds = 15`.
- Them cau hinh an toan `MinimumHiddenLettersBeforeReveal = 2`.
- Doi masked word theo yeu cau sync hint: giu khoang trang that, chu cai bi che bang `_`.
- Them `BuildMaskedWord()` de tao masked word theo tap vi tri da reveal.

### GameServer/Rooms/GameRoom.cs

- Them `CurrentMaskedWord` va tap vi tri chu da reveal.
- Khi drawer chon tu, server khoi tao masked word bi che.
- Them `RevealRandomMaskedLetter()`:
  - chi chay khi room dang `Drawing`;
  - quet cac vi tri chu cai con bi che;
  - bo qua neu chi con <= 2 chu cai bi che;
  - random 1 vi tri va cap nhat `CurrentMaskedWord`.
- Them `RoundVersion` de timer cua round cu khong bi tiep tuc broadcast khi round moi bat dau.

### GameServer/Managers/RoomManager.cs

- Tra `CurrentMaskedWord` tu room thay vi tao mask roi bo trang thai.
- Them `RevealHintLetter()` de GatewayHandler co the kich hoat sync hint dinh ky.
- Tra `RoundVersion` trong ket qua select word de khoa timer loop theo dung round.

### GameServer/Handlers/GatewayHandler.cs

- Sau `SelectWord`, server gui hint ban dau cho guessers voi `maskedWord`.
- Thay timer delay 60s bang round loop:
  - broadcast `TimerUpdate` moi giay;
  - moi 15 giay goi reveal hint va gui `MessageType.Hint` cho guessers;
  - het 60 giay thi `RoundEnd`;
  - tu dung neu round da ket thuc hoac `RoundVersion` khong con khop.

### scripts/smoke-stage5.ps1

- Them smoke test Stage 5:
  - start Gateway + GameServer tren port tam;
  - tao 2 TLS clients;
  - register/login/create/join/ready/select word;
  - drawer gui `DRAW`, guesser nhan `DRAW_DATA`;
  - guesser gui `DRAW` bi reject bang `ERROR`;
  - drawer gui clear canvas, guesser nhan `DRAW_DATA` voi `color = CLEAR`;
  - doi 15 giay va verify server gui `Hint` moi co lật chu cai.

## Verification da chay

```powershell
dotnet build .\PictionaryOnline.sln
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-stage5.ps1 -GatewayPort 5714 -GameServerPort 6714
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-stage4.ps1 -GatewayPort 5715 -GameServerPort 6715
```

Ket qua:

```text
dotnet build: succeeded, 0 warnings, 0 errors
smoke-stage5.ps1: STAGE5_SMOKE_PASS
smoke-stage4.ps1: STAGE4_SMOKE_PASS
```
