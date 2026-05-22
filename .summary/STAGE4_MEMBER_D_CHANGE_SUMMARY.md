# Stage 4 Member D Change Summary

## Goal

Member D scope was updated so WordBank is now the source of drawable words, while Gemini is used only to generate creative hints.

The Gemini API key is not hardcoded. It can be provided through an environment variable or through a local ignored secret file.

Reference used for the REST shape: Google AI Gemini `generateContent` docs, which show `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent` with `x-goog-api-key`.

Docs: https://ai.google.dev/gemini-api/docs

## Configuration

Recommended local setup:

```powershell
Copy-Item .\GameServer\secrets\gemini-api-key.example.txt .\GameServer\secrets\gemini-api-key.txt
notepad .\GameServer\secrets\gemini-api-key.txt
```

Alternative setup:

```powershell
$env:GEMINI_API_KEY = "your-api-key"
```

Optional model override:

```powershell
$env:GEMINI_MODEL = "gemini-2.5-flash"
```

The local key file is ignored by git:

```text
GameServer/secrets/gemini-api-key.txt
```

## Detailed Summary By File

### .gitignore
- Added `.env.*` ignore while keeping `.env.example` trackable.
- Added `GameServer/secrets/*` ignore rules.
- Kept `GameServer/secrets/*.example.txt` and `GameServer/secrets/README.md` trackable so sample secret docs can be committed safely.

### GameServer/Services/WordBankService.cs
- Expanded the word bank from 25 words to more than 100 Vietnamese-friendly drawable words.
- Added `WordCount` for quick sanity checks.
- Added `GetWords(count)` as the main word source for gameplay.
- Kept `GetFallbackWords(count)` as a compatibility wrapper.

### GameServer/Services/GeminiService.cs
- Replaced word generation placeholder with real Gemini hint generation.
- Reads key from `GEMINI_API_KEY`, `GOOGLE_API_KEY`, `GEMINI_API_KEY_FILE`, or local `GameServer/secrets/gemini-api-key.txt`.
- Uses `GEMINI_MODEL` when provided, otherwise defaults to `gemini-2.5-flash`.
- Calls Gemini REST `generateContent` with `x-goog-api-key`.
- Sets `thinkingConfig.thinkingBudget = 0` for short hint generation, avoiding very short/truncated outputs caused by thinking tokens.
- Builds a Vietnamese prompt that asks for one short, indirect, creative Pictionary hint.
- Parses model output defensively: if Gemini returns a preamble or numbered list, the service selects the first valid hint line.
- Cleans Gemini output and rejects hints that contain the secret word or obvious word tokens.
- Retries lightly on `429 Too Many Requests`.
- Caches hints per word inside the GameServer process to reduce repeated API calls.
- Falls back to a rotating set of safe creative hints if key is missing, API fails, rate limit is hit, or the generated hint is too close to the answer.

### GameServer/Engine/GameEngine.cs
- Changed `GetWordOptionsAsync` to always use WordBank instead of Gemini.
- Added `GenerateHintAsync` to delegate hint generation to `GeminiService`.
- Existing mask, guess matching, and scoring logic remains unchanged.

### GameServer/Managers/RoomManager.cs
- Changed word selection flow to async so it can request a Gemini hint.
- `WordSelectedResult` now contains both `Hint` and `MaskedWord`.
- The selected word is still stored in the room first, then hint/mask payload data is prepared for clients.

### GameServer/Handlers/GatewayHandler.cs
- Updated `SelectWord` handling to call `SelectWordAsync`.
- Sends Gemini-generated creative hint as `hint`.
- Sends masked answer pattern separately as `maskedWord`.
- Existing targeted delivery remains the same: only guessers receive the hint payload.

### GameServer/secrets/gemini-api-key.example.txt
- Added a safe placeholder file to show where the local API key should be placed.
- The real `gemini-api-key.txt` file is ignored by `.gitignore`.

### scripts/test-gemini-hint.ps1
- Added a standalone Gemini hint test script for later use after an API key is configured.
- Creates a temporary .NET console project, references `GameServer`, and calls `GeminiService.GenerateHintAsync`.
- Verifies that the hint is non-empty and does not contain the full secret word.
- Also rejects hints that are too short or contain obvious word tokens from the answer.

### context/STAGE4_MEMBER_D_CHANGE_SUMMARY.md
- Added this summary document for PR/write-up support.

## One-Line PR Description Notes

- `.gitignore`: Ignore local dotenv variants and GameServer secret files while keeping safe examples trackable.
- `GameServer/Services/WordBankService.cs`: Expand WordBank to 100+ drawable Vietnamese words and make it the main word source.
- `GameServer/Services/GeminiService.cs`: Use Gemini only for creative hint generation with safe key loading, thinking disabled, output cleanup, retry/cache, and fallback behavior.
- `GameServer/Engine/GameEngine.cs`: Switch word options to WordBank and expose Gemini-backed hint generation.
- `GameServer/Managers/RoomManager.cs`: Make word selection async and return both creative hint and masked word.
- `GameServer/Handlers/GatewayHandler.cs`: Send Gemini hint plus masked word to guessers after drawer selects a word.
- `GameServer/secrets/gemini-api-key.example.txt`: Add a placeholder secret file template for local Gemini API key setup.
- `scripts/test-gemini-hint.ps1`: Add a manual Gemini hint test script for use after configuring an API key.
- `context/STAGE4_MEMBER_D_CHANGE_SUMMARY.md`: Document Stage 4 member D changes and setup instructions.

## Manual Test After Adding API Key

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-gemini-hint.ps1 -Word "bánh mì"
```

Fast gameplay smoke test without waiting for natural timer expiry:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-stage4.ps1 -SkipNaturalExpiry
```

Full gameplay smoke test:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-stage4.ps1
```

## Verification Done Now

Gemini script was run after API key setup. During repeated calls, Gemini may return `429 Too Many Requests`; the service now retries briefly and falls back safely.

```text
dotnet build: succeeded with 0 warnings and 0 errors
test-gemini-hint.ps1: passed with configured key
smoke-stage4.ps1 -SkipNaturalExpiry: STAGE4_SMOKE_PASS
```
