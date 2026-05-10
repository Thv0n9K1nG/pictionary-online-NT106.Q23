param(
    [int]$GatewayPort = 5701,
    [int]$GameServerPort = 6701,
    [switch]$SkipNaturalExpiry
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tempRoot = Join-Path $env:TEMP ('pictionary-stage4-smoke-' + [guid]::NewGuid().ToString('N'))
$gatewayWork = Join-Path $tempRoot 'gateway'
$gameWork = Join-Path $tempRoot 'gameserver'
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

function Start-Stage4Servers {
    $gatewayArgs = @(
        'run', '--no-build',
        '--project', (Join-Path $root 'Gateway/Gateway.csproj'),
        '--',
        $GatewayPort,
        $GameServerPort
    )

    $gameArgs = @(
        'run', '--no-build',
        '--project', (Join-Path $root 'GameServer/GameServer.csproj'),
        '--',
        'stage4-smoke-gs1',
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
    } | ConvertTo-Json -Compress -Depth 10
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

try {
    Start-Stage4Servers

    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $password = 'stage4pw'
    $hostClient = New-SmokeClient 'host'
    $guestClient = New-SmokeClient 'guest'
    $hostLogin = Register-And-Login $hostClient "s4host$suffix" $password
    $guestLogin = Register-And-Login $guestClient "s4guest$suffix" $password

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
    if ($words.Count -ne 5) {
        throw "Expected 5 word options, got $($words.Count)."
    }

    $selectedWord = [string]$words[0]
    Send-SmokeMessage $drawer 'SelectWord' @{
        roomCode = $roomCode
        sessionId = $drawerSession
        word = $selectedWord
    }
    $hint = Read-UntilType $guesser 26
    if ([string]::IsNullOrWhiteSpace([string]$hint.payload.hint)) {
        throw 'Hint payload is empty.'
    }
    [void](Read-UntilType $hostClient 23)
    [void](Read-UntilType $guestClient 23)

    Send-SmokeMessage $guesser 'Guess' @{
        roomCode = $roomCode
        sessionId = $guesserSession
        guess = "  $($selectedWord.ToUpperInvariant())  "
    }
    [void](Read-UntilType $hostClient 22)
    [void](Read-UntilType $guestClient 22)
    [void](Read-UntilType $hostClient 18)
    [void](Read-UntilType $guestClient 18)
    [void](Read-UntilType $hostClient 24)
    [void](Read-UntilType $guestClient 24)

    if (-not $SkipNaturalExpiry) {
        Send-SmokeMessage $hostClient 'Ready' @{
            roomCode = $roomCode
            sessionId = $hostLogin.SessionId
        }
        $roundTwoHost = Read-UntilType $hostClient 19
        $roundTwoGuest = Read-UntilType $guestClient 19
        $drawerTwoId = [string]$roundTwoHost.payload.drawerId
        if ($drawerTwoId -ne [string]$roundTwoGuest.payload.drawerId) {
            throw 'Second GameStart drawer mismatch.'
        }

        $drawerTwo = if ($drawerTwoId -eq $hostLogin.PlayerId) { $hostClient } elseif ($drawerTwoId -eq $guestLogin.PlayerId) { $guestClient } else { throw "Unknown drawer $drawerTwoId." }
        $drawerTwoSession = if ($drawerTwo -eq $hostClient) { $hostLogin.SessionId } else { $guestLogin.SessionId }
        $guesserTwo = if ($drawerTwo -eq $hostClient) { $guestClient } else { $hostClient }

        $optionsTwo = Read-UntilType $drawerTwo 20
        $wordTwo = [string]@($optionsTwo.payload.words)[0]
        Send-SmokeMessage $drawerTwo 'SelectWord' @{
            roomCode = $roomCode
            sessionId = $drawerTwoSession
            word = $wordTwo
        }
        [void](Read-UntilType $guesserTwo 26)
        [void](Read-UntilType $hostClient 23)
        [void](Read-UntilType $guestClient 23)

        Write-Host 'Waiting for natural 60-second round expiry...'
        [void](Read-UntilType $hostClient 24 70000)
        [void](Read-UntilType $guestClient 24 70000)
        [void](Read-UntilType $hostClient 25 5000)
        [void](Read-UntilType $guestClient 25 5000)
    }

    Write-Host 'STAGE4_SMOKE_PASS'
}
catch {
    Write-Host 'STAGE4_SMOKE_FAIL'
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
