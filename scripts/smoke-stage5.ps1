param(
    [int]$GatewayPort = 5702,
    [int]$GameServerPort = 6702
)

# Stage 5 smoke test.
# Member B: client canvas emits pen/shape/clear payloads and replays DRAW_DATA.
# Member A: Gateway/GameServer route, validate, and broadcast DRAW commands authoritatively.

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tempRoot = Join-Path $env:TEMP ('pictionary-stage5-smoke-' + [guid]::NewGuid().ToString('N'))
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

function Start-Stage5Servers {
    dotnet build (Join-Path $root 'PictionaryOnline.sln') -p:BaseOutputPath="$buildRoot\" -p:UseAppHost=false | Out-Host

    $gatewayDll = Join-Path $buildRoot 'Debug\net8.0\Gateway.dll'
    $gameServerDll = Join-Path $buildRoot 'Debug\net8.0\GameServer.dll'

    $gatewayArgs = @(
        $gatewayDll,
        $GatewayPort,
        $GameServerPort
    )

    $gameArgs = @(
        $gameServerDll,
        'stage5-smoke-gs1',
        '127.0.0.1',
        $GameServerPort
    )

    $script:gateway = Start-Process -FilePath 'dotnet' -ArgumentList $gatewayArgs -WorkingDirectory $gatewayWork -PassThru -WindowStyle Hidden -RedirectStandardOutput $gatewayOut -RedirectStandardError $gatewayErr
    Start-Sleep -Seconds 2
    $script:game = Start-Process -FilePath 'dotnet' -ArgumentList $gameArgs -WorkingDirectory $gameWork -PassThru -WindowStyle Hidden -RedirectStandardOutput $gameOut -RedirectStandardError $gameErr
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

function Get-LetterCount {
    param([string]$Value)
    return (($Value.ToCharArray() | Where-Object { -not [char]::IsWhiteSpace($_) }).Count)
}

try {
    Start-Stage5Servers

    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $password = 'stage5pw'
    $hostClient = New-SmokeClient 'host'
    $guestClient = New-SmokeClient 'guest'
    $outsiderClient = New-SmokeClient 'outsider'
    $hostLogin = Register-And-Login $hostClient "s5host$suffix" $password
    $guestLogin = Register-And-Login $guestClient "s5guest$suffix" $password
    $outsiderLogin = Register-And-Login $outsiderClient "s5outsider$suffix" $password

    # Shared setup: create one playable room with two members.
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

    Send-SmokeMessage $hostClient 'Ready' @{
        roomCode = $roomCode
        sessionId = $hostLogin.SessionId
    }
    $startHost = Read-UntilType $hostClient 19
    $startGuest = Read-UntilType $guestClient 19
    $drawerId = [string]$startHost.payload.drawerId
    if ($drawerId -ne [string]$startGuest.payload.drawerId) {
        throw 'GameStart drawer mismatch.'
    }

    $drawer = if ($drawerId -eq $hostLogin.PlayerId) { $hostClient } elseif ($drawerId -eq $guestLogin.PlayerId) { $guestClient } else { throw "Unknown drawer $drawerId." }
    $drawerSession = if ($drawer -eq $hostClient) { $hostLogin.SessionId } else { $guestLogin.SessionId }
    $guesser = if ($drawer -eq $hostClient) { $guestClient } else { $hostClient }
    $guesserSession = if ($guesser -eq $hostClient) { $hostLogin.SessionId } else { $guestLogin.SessionId }

    $options = Read-UntilType $drawer 20
    $words = @($options.payload.words)
    $selectedWord = [string]($words | Where-Object { (Get-LetterCount $_) -gt 3 } | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($selectedWord)) {
        $selectedWord = [string]$words[0]
    }

    Send-SmokeMessage $drawer 'SelectWord' @{
        roomCode = $roomCode
        sessionId = $drawerSession
        word = $selectedWord
    }

    $initialHint = Read-UntilType $guesser 26
    $initialMasked = [string]$initialHint.payload.maskedWord
    if ([string]::IsNullOrWhiteSpace($initialMasked) -or $initialMasked -notmatch '_') {
        throw 'Initial masked hint is missing underscores.'
    }

    # Member A/B: a valid drawer stroke must reach guessers as DRAW_DATA.
    Send-SmokeMessage $drawer 'Draw' @{
        roomCode = $roomCode
        sessionId = $drawerSession
        drawPayload = @{
            x1 = 10
            y1 = 20
            x2 = 120
            y2 = 130
            color = '#FF0000'
            brushSize = 6
            isEraser = $false
            tool = 'Pen'
        }
    }
    $drawData = Read-UntilType $guesser 21
    if ([string]$drawData.payload.drawPayload.color -ne '#FF0000') {
        throw 'Guesser did not receive the drawer stroke.'
    }

    # Member B payload + Member A relay: shape tools must survive the network round-trip.
    Send-SmokeMessage $drawer 'Draw' @{
        roomCode = $roomCode
        sessionId = $drawerSession
        drawPayload = @{
            x1 = 30
            y1 = 40
            x2 = 180
            y2 = 140
            color = '#0000FF'
            brushSize = 6
            isEraser = $false
            tool = 'Rectangle'
        }
    }
    $shapeData = Read-UntilType $guesser 21
    if ([string]$shapeData.payload.drawPayload.tool -ne 'Rectangle') {
        throw 'Shape drawing command did not preserve the selected tool.'
    }

    # Member A: a logged-in session that never joined the room must be rejected at Gateway.
    Send-SmokeMessage $outsiderClient 'Draw' @{
        roomCode = $roomCode
        sessionId = $outsiderLogin.SessionId
        drawPayload = @{
            x1 = 5
            y1 = 5
            x2 = 25
            y2 = 25
            color = '#00AA00'
            brushSize = 4
            isEraser = $false
            tool = 'Pen'
        }
    }
    $outsiderError = Read-UntilType $outsiderClient 29
    if ([string]$outsiderError.payload.message -ne 'Session is not in this room.') {
        throw 'Gateway did not reject an out-of-room draw session.'
    }

    # Member A: guessers are room members, but they still cannot draw during someone else's turn.
    Send-SmokeMessage $guesser 'Draw' @{
        roomCode = $roomCode
        sessionId = $guesserSession
        drawPayload = @{
            x1 = 1
            y1 = 1
            x2 = 2
            y2 = 2
            color = '#000000'
            brushSize = 2
            isEraser = $false
            tool = 'Pen'
        }
    }
    [void](Read-UntilType $guesser 29)

    # Member B/A: clear canvas uses the same DRAW_DATA path so every guesser clears together.
    Send-SmokeMessage $drawer 'Draw' @{
        roomCode = $roomCode
        sessionId = $drawerSession
        drawPayload = @{
            x1 = 0
            y1 = 0
            x2 = 0
            y2 = 0
            color = 'CLEAR'
            brushSize = 0
            isEraser = $false
            tool = 'Pen'
        }
    }
    $clearData = Read-UntilType $guesser 21
    if ([string]$clearData.payload.drawPayload.color -ne 'CLEAR') {
        throw 'Clear canvas command was not broadcast as DRAW_DATA.'
    }

    # Sync hint: GameServer owns timed letter reveal and sends updated maskedWord to guessers.
    if ((Get-LetterCount $selectedWord) -gt 3) {
        $reveal = Read-UntilType $guesser 26 20000
        $revealedMasked = [string]$reveal.payload.maskedWord
        if ($revealedMasked -eq $initialMasked -or $revealedMasked -notmatch '[^\s_]') {
            throw 'Timed sync hint did not reveal a letter.'
        }
    }

    Write-Host 'STAGE5_SMOKE_PASS'
}
catch {
    Write-Host 'STAGE5_SMOKE_FAIL'
    Write-Host $_.Exception.Message
    Write-Host '--- gateway stdout ---'
    if (Test-Path $gatewayOut) { Get-Content $gatewayOut -Tail 200 }
    Write-Host '--- gateway stderr ---'
    if (Test-Path $gatewayErr) { Get-Content $gatewayErr -Tail 200 }
    Write-Host '--- gameserver stdout ---'
    if (Test-Path $gameOut) { Get-Content $gameOut -Tail 200 }
    Write-Host '--- gameserver stderr ---'
    if (Test-Path $gameErr) { Get-Content $gameErr -Tail 200 }
    throw
}
finally {
    Stop-SmokeProcesses
    Write-Host "Logs: $tempRoot"
}
