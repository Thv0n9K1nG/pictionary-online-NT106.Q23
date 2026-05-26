param(
    [int]$GatewayPort = 5806,
    [int]$GameServerPort = 6806
)

# Stage 6 member D smoke test: GAME_END must persist Matches, MatchPlayers and PlayerStats.

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tempRoot = Join-Path $env:TEMP ('pictionary-stage6-D-smoke-' + [guid]::NewGuid().ToString('N'))
$gatewayWork = Join-Path $tempRoot 'gateway'
$gameWork = Join-Path $tempRoot 'gameserver'
$buildRoot = Join-Path $tempRoot 'build'
New-Item -ItemType Directory -Path $gatewayWork -Force | Out-Null
New-Item -ItemType Directory -Path $gameWork -Force | Out-Null

$gatewayOut = Join-Path $tempRoot 'gateway.out.log'
$gatewayErr = Join-Path $tempRoot 'gateway.err.log'
$gameOut = Join-Path $tempRoot 'gameserver.out.log'
$gameErr = Join-Path $tempRoot 'gameserver.err.log'

$clients = New-Object System.Collections.Generic.List[object]
$gateway = $null
$game = $null

function Stop-SmokeProcesses {
    foreach ($client in $clients) {
        try { $client.Stream.Dispose() } catch { }
        try { $client.Tcp.Close() } catch { }
    }

    foreach ($proc in @($game, $gateway)) {
        if ($null -ne $proc -and -not $proc.HasExited) {
            try { Stop-Process -Id $proc.Id -Force } catch { }
        }
    }
}

function Start-Stage6Servers {
    dotnet build (Join-Path $root 'PictionaryOnline.sln') -p:BaseOutputPath="$buildRoot\" -p:UseAppHost=false | Out-Host

    $gatewayDll = Join-Path $buildRoot 'Debug\net8.0\Gateway.dll'
    $gameServerDll = Join-Path $buildRoot 'Debug\net8.0\GameServer.dll'

    $script:gateway = Start-Process -FilePath 'dotnet' -ArgumentList @($gatewayDll, $GatewayPort, $GameServerPort) -WorkingDirectory $gatewayWork -PassThru -WindowStyle Hidden -RedirectStandardOutput $gatewayOut -RedirectStandardError $gatewayErr
    Start-Sleep -Seconds 2
    $script:game = Start-Process -FilePath 'dotnet' -ArgumentList @($gameServerDll, 'stage6-D-smoke-gs1', '127.0.0.1', $GameServerPort) -WorkingDirectory $gameWork -PassThru -WindowStyle Hidden -RedirectStandardOutput $gameOut -RedirectStandardError $gameErr
    Start-Sleep -Seconds 4
}

function New-SmokeClient {
    param([string]$Name)

    $tcp = [System.Net.Sockets.TcpClient]::new()
    $tcp.Connect('127.0.0.1', $GatewayPort)

    $callback = [System.Net.Security.RemoteCertificateValidationCallback]{
        param($sender, $certificate, $chain, $sslPolicyErrors)
        return $true
    }
    $ssl = [System.Net.Security.SslStream]::new($tcp.GetStream(), $false, $callback)
    $ssl.AuthenticateAsClient('localhost')

    $reader = [System.IO.StreamReader]::new($ssl, [System.Text.Encoding]::UTF8, $false, 1024, $true)
    $writer = [System.IO.StreamWriter]::new($ssl, [System.Text.Encoding]::UTF8, 1024, $true)
    $writer.AutoFlush = $true

    $client = [pscustomobject]@{
        Name = $Name
        Tcp = $tcp
        Stream = $ssl
        Reader = $reader
        Writer = $writer
    }
    $clients.Add($client) | Out-Null
    return $client
}

function Send-SmokeMessage {
    param($Client, [string]$Type, $Payload)

    $line = @{
        type = $Type
        payload = $Payload
    } | ConvertTo-Json -Compress -Depth 12
    $Client.Writer.WriteLine($line)
}

function Read-SmokeLine {
    param($Client, [int]$TimeoutMs = 10000)

    $task = $Client.Reader.ReadLineAsync()
    if (-not $task.Wait($TimeoutMs)) {
        throw "Timeout waiting for message from $($Client.Name)."
    }
    if ($null -eq $task.Result) {
        throw "$($Client.Name) disconnected."
    }
    return $task.Result
}

function Read-UntilType {
    param($Client, [int]$Type, [int]$TimeoutMs = 10000)

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        $remaining = [int][Math]::Max(1, ($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        $line = Read-SmokeLine $Client $remaining
        $message = $line | ConvertFrom-Json
        Write-Host "[$($Client.Name)] <= type=$($message.type) payload=$($message.payload | ConvertTo-Json -Compress -Depth 8)"
        if ([int]$message.type -eq $Type) {
            return $message
        }
    }

    throw "Did not receive message type $Type from $($Client.Name)."
}

function Register-And-Login {
    param($Client, [string]$Username, [string]$Password)

    Send-SmokeMessage $Client 'Register' @{ username = $Username; password = $Password }
    [void](Read-UntilType $Client 12)

    Send-SmokeMessage $Client 'Login' @{ username = $Username; password = $Password }
    $login = Read-UntilType $Client 14
    return [pscustomobject]@{
        SessionId = [string]$login.payload.sessionId
        PlayerId = [string]$login.payload.playerId
    }
}

function Play-One-Round {
    param(
        $HostClient,
        $GuestClient,
        [string]$RoomCode,
        [string]$HostSessionId,
        [string]$GuestSessionId,
        [string]$HostPlayerId,
        [string]$GuestPlayerId
    )

    Send-SmokeMessage $HostClient 'Ready' @{
        roomCode = $RoomCode
        sessionId = $HostSessionId
    }

    $startHost = Read-UntilType $HostClient 19
    $startGuest = Read-UntilType $GuestClient 19
    $drawerId = [string]$startHost.payload.drawerId
    if ($drawerId -ne [string]$startGuest.payload.drawerId) {
        throw 'GameStart drawer mismatch.'
    }

    $drawer = if ($drawerId -eq $HostPlayerId) { $HostClient } elseif ($drawerId -eq $GuestPlayerId) { $GuestClient } else { throw "Unknown drawer $drawerId." }
    $drawerSession = if ($drawer -eq $HostClient) { $HostSessionId } else { $GuestSessionId }
    $guesser = if ($drawer -eq $HostClient) { $GuestClient } else { $HostClient }
    $guesserSession = if ($guesser -eq $HostClient) { $HostSessionId } else { $GuestSessionId }

    $options = Read-UntilType $drawer 20
    $word = [string]@($options.payload.words)[0]

    Send-SmokeMessage $drawer 'SelectWord' @{
        roomCode = $RoomCode
        sessionId = $drawerSession
        word = $word
    }

    [void](Read-UntilType $guesser 26)
    [void](Read-UntilType $HostClient 23)
    [void](Read-UntilType $GuestClient 23)

    Send-SmokeMessage $guesser 'Guess' @{
        roomCode = $RoomCode
        sessionId = $guesserSession
        guess = $word
    }

    [void](Read-UntilType $HostClient 22)
    [void](Read-UntilType $GuestClient 22)
    [void](Read-UntilType $HostClient 18)
    [void](Read-UntilType $GuestClient 18)
    return Read-UntilType $HostClient 24
}

function Assert-Stage6Database {
    param([string]$DatabasePath, [string]$RoomCode)

    $queryRoot = Join-Path $tempRoot 'db-query'
    New-Item -ItemType Directory -Path $queryRoot -Force | Out-Null
    Push-Location $queryRoot
    try {
        dotnet new console --framework net8.0 --force | Out-Null
        dotnet add package Microsoft.Data.Sqlite --version 10.0.7 | Out-Null

        Set-Content -Path (Join-Path $queryRoot 'Program.cs') -Encoding UTF8 -Value @'
using Microsoft.Data.Sqlite;

var dbPath = args[0];
var roomCode = args[1];
await using var connection = new SqliteConnection($"Data Source={dbPath}");
await connection.OpenAsync();

var matchCount = await ScalarAsync(connection, "SELECT COUNT(*) FROM Matches WHERE roomCode = $roomCode AND status = 'COMPLETED'", roomCode);
var matchPlayerCount = await ScalarAsync(connection, """
SELECT COUNT(*)
FROM MatchPlayers
WHERE matchId IN (SELECT matchId FROM Matches WHERE roomCode = $roomCode)
  AND displayName <> ''
""", roomCode);
var statsCount = await ScalarAsync(connection, """
SELECT COUNT(*)
FROM PlayerStats
WHERE userId IN (
    SELECT userId
    FROM MatchPlayers
    WHERE matchId IN (SELECT matchId FROM Matches WHERE roomCode = $roomCode)
)
AND totalMatches >= 1
""", roomCode);
var winnerCount = await ScalarAsync(connection, """
SELECT COUNT(*)
FROM MatchPlayers
WHERE matchId IN (SELECT matchId FROM Matches WHERE roomCode = $roomCode)
  AND isWinner = 1
""", roomCode);

Console.WriteLine($"matches={matchCount}; matchPlayers={matchPlayerCount}; stats={statsCount}; winners={winnerCount}");

if (matchCount < 1 || matchPlayerCount < 2 || statsCount < 2 || winnerCount < 1)
{
    return 2;
}

return 0;

static async Task<long> ScalarAsync(SqliteConnection connection, string sql, string roomCode)
{
    var command = connection.CreateCommand();
    command.CommandText = sql;
    command.Parameters.AddWithValue("$roomCode", roomCode);
    return (long)(await command.ExecuteScalarAsync() ?? 0L);
}
'@

        dotnet run -- $DatabasePath $RoomCode
    }
    finally {
        Pop-Location
    }
}

function Assert-Stage6ClientQueries {
    param(
        $Client,
        [string]$SessionId,
        [string]$RoomCode
    )

    # Stage 6 - C: client stats/history requests should be served by Gateway from persisted data.
    Send-SmokeMessage $Client 'GetPlayerStats' @{
        sessionId = $SessionId
    }
    $stats = Read-UntilType $Client 33
    if ([int]$stats.payload.totalMatches -lt 1 -or [int]$stats.payload.totalScore -lt 1) {
        throw "Player stats were not updated after the completed match."
    }

    Send-SmokeMessage $Client 'GetMatchHistory' @{
        sessionId = $SessionId
        limit = 5
    }
    $history = Read-UntilType $Client 32
    $matches = @($history.payload)
    if ($matches.Count -lt 1 -or -not ($matches | Where-Object { [string]$_.roomCode -eq $RoomCode })) {
        throw "Match history did not include room $RoomCode."
    }
}

function Assert-Stage6Reconnect {
    param(
        [string]$SessionId
    )

    # Stage 6 - C: reconnect should re-bind an existing session and return room recovery info.
    $reconnectClient = New-SmokeClient 'reconnect'
    Send-SmokeMessage $reconnectClient 'Reconnect' @{
        sessionId = $SessionId
    }
    [void](Read-UntilType $reconnectClient 28)
}

try {
    Start-Stage6Servers

    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $password = 'stage6pw'
    $hostClient = New-SmokeClient 'host'
    $guestClient = New-SmokeClient 'guest'
    $hostLogin = Register-And-Login $hostClient "s6host$suffix" $password
    $guestLogin = Register-And-Login $guestClient "s6guest$suffix" $password

    Send-SmokeMessage $hostClient 'CreateRoom' @{
        playerName = 'Host Smoke'
        sessionId = $hostLogin.SessionId
    }
    $created = Read-UntilType $hostClient 17
    $roomCode = [string]$created.payload.roomCode
    Write-Host "Created room $roomCode"

    Send-SmokeMessage $guestClient 'Join' @{
        roomCode = $roomCode
        playerName = 'Guest Smoke'
        sessionId = $guestLogin.SessionId
    }
    [void](Read-UntilType $guestClient 17)
    [void](Read-UntilType $hostClient 18)

    # Stage 6 - D setup: finish a two-player game quickly so GAME_END produces MATCH_RESULT.
    $roundOne = Play-One-Round $hostClient $guestClient $roomCode $hostLogin.SessionId $guestLogin.SessionId $hostLogin.PlayerId $guestLogin.PlayerId
    if ([bool]$roundOne.payload.gameEnded) {
        throw 'Game ended too early after one round.'
    }

    $roundTwo = Play-One-Round $hostClient $guestClient $roomCode $hostLogin.SessionId $guestLogin.SessionId $hostLogin.PlayerId $guestLogin.PlayerId
    if (-not [bool]$roundTwo.payload.gameEnded) {
        throw 'Game did not end after all players drew once.'
    }

    [void](Read-UntilType $hostClient 25)
    [void](Read-UntilType $guestClient 25)
    Start-Sleep -Seconds 2

    $databasePath = Join-Path $gatewayWork 'pictionary.db'
    if (-not (Test-Path $databasePath)) {
        throw "Database not found at $databasePath"
    }

    # Stage 6 - D verification: SQLite has match header, player rows and aggregate stats.
    Assert-Stage6Database $databasePath $roomCode
    Assert-Stage6ClientQueries $hostClient $hostLogin.SessionId $roomCode
    Assert-Stage6Reconnect $hostLogin.SessionId

    Write-Host 'STAGE6_D_SMOKE_PASS'
}
catch {
    Write-Host 'STAGE6_D_SMOKE_FAIL'
    Write-Host $_.Exception.Message
    Write-Host '--- gateway stdout ---'
    if (Test-Path $gatewayOut) { Get-Content $gatewayOut -Tail 240 }
    Write-Host '--- gateway stderr ---'
    if (Test-Path $gatewayErr) { Get-Content $gatewayErr -Tail 240 }
    Write-Host '--- gameserver stdout ---'
    if (Test-Path $gameOut) { Get-Content $gameOut -Tail 240 }
    Write-Host '--- gameserver stderr ---'
    if (Test-Path $gameErr) { Get-Content $gameErr -Tail 240 }
    throw
}
finally {
    Stop-SmokeProcesses
    Write-Host "Logs: $tempRoot"
}
