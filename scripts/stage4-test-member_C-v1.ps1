param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

function Assert-FileContains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not (Test-Path $Path)) {
        throw "Missing file: $Path"
    }

    $content = Get-Content -Encoding UTF8 -Raw $Path
    if ($content -notmatch $Pattern) {
        throw $Message
    }
}

Write-Host "[stage4-member-C] Building solution..."
dotnet build .\PictionaryOnline.sln --no-restore --configuration $Configuration -p:BaseOutputPath=artifacts\stage4-member-C-test\

Write-Host "[stage4-member-C] Checking client gameplay UI implementation..."
Assert-FileContains "Client\UI\GameForm.cs" "GameMessageFactory\.Ready" "GameForm must send READY with room/session payload."
Assert-FileContains "Client\UI\GameForm.cs" "GameMessageFactory\.SelectWord" "GameForm must send SELECT_WORD with room/session payload."
Assert-FileContains "Client\UI\GameForm.cs" "GameMessageFactory\.Guess" "GameForm must send GUESS with room/session payload."
Assert-FileContains "Client\UI\GameForm.cs" "WordOptionsReceived\s*\+=" "GameForm must show word options for the drawer."
Assert-FileContains "Client\UI\GameForm.cs" "RoundEnded\s*\+=" "GameForm must handle round result events."
Assert-FileContains "Client\UI\GameForm.cs" "GameEnded\s*\+=" "GameForm must handle game result events."
Assert-FileContains "Client\UI\GameForm.cs" "_scoreboard" "GameForm must keep a scoreboard control."
Assert-FileContains "Client\UI\GameForm.cs" "_lblTimer" "GameForm must render the round timer."
Assert-FileContains "Client\UI\GameForm.cs" "_txtGuess" "GameForm must provide guess input."

Write-Host "[stage4-member-C] Checking dispatcher coverage..."
Assert-FileContains "Client\Services\MessageDispatcher.cs" "case MessageType\.WordOptions" "Dispatcher must handle WORD_OPTIONS."
Assert-FileContains "Client\Services\MessageDispatcher.cs" "case MessageType\.TimerUpdate" "Dispatcher must handle TIMER_UPDATE."
Assert-FileContains "Client\Services\MessageDispatcher.cs" "case MessageType\.CorrectGuess" "Dispatcher must handle CORRECT_GUESS."
Assert-FileContains "Client\Services\MessageDispatcher.cs" "case MessageType\.RoundEnd" "Dispatcher must handle ROUND_END."
Assert-FileContains "Client\Services\MessageDispatcher.cs" "case MessageType\.GameEnd" "Dispatcher must handle GAME_END."
Assert-FileContains "Client\Services\MessageDispatcher.cs" "PlayerListUpdated\?\.Invoke" "Dispatcher must update/render scoreboard data."

Write-Host "[stage4-member-C] Checking message factories and result UI..."
Assert-FileContains "Client\Services\GameMessageFactory.cs" "Ready\(string roomCode, string sessionId\)" "GameMessageFactory must create READY messages."
Assert-FileContains "Client\Services\GameMessageFactory.cs" "SelectWord\(string roomCode, string word, string sessionId\)" "GameMessageFactory must create SELECT_WORD messages."
Assert-FileContains "Client\Services\GameMessageFactory.cs" "Guess\(string roomCode, string guess, string sessionId\)" "GameMessageFactory must create GUESS messages."
Assert-FileContains "Client\UI\ResultForm.cs" "ListBox" "ResultForm must render round/game scores."

Write-Host "[stage4-member-C] Checking IsDrawer compatibility with server player list..."
Assert-FileContains "GameServer\Managers\RoomManager.cs" "new PlayerInfo\([^)]*false\)" "RoomManager must construct PlayerInfo with IsDrawer."
Assert-FileContains "GameServer\Rooms\GameRoom.cs" "PlayersWithDrawerFlag" "GameRoom must expose drawer flags in player lists."

Write-Host "[stage4-member-C] All checks passed."
