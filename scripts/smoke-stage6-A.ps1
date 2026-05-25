param(
    [int]$GatewayPort = 5807,
    [int]$GameServerPort = 6807
)

# Stage 6 - A: checkpoint recovery smoke test for GameServer failover.

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tempRoot = Join-Path $env:TEMP ('pictionary-stage6-A-smoke-' + [guid]::NewGuid().ToString('N'))
$gatewayWork = Join-Path $tempRoot 'gateway'
$gs1Work = Join-Path $tempRoot 'gs1'
$gs2Work = Join-Path $tempRoot 'gs2'
$buildRoot = Join-Path $tempRoot 'build'
New-Item -ItemType Directory -Path $gatewayWork, $gs1Work, $gs2Work -Force | Out-Null

$gatewayOut = Join-Path $tempRoot 'gateway.out.log'
$gatewayErr = Join-Path $tempRoot 'gateway.err.log'
$gs1Out = Join-Path $tempRoot 'gs1.out.log'
$gs1Err = Join-Path $tempRoot 'gs1.err.log'
$gs2Out = Join-Path $tempRoot 'gs2.out.log'
$gs2Err = Join-Path $tempRoot 'gs2.err.log'

$clients = New-Object System.Collections.Generic.List[object]
$gateway = $null
$gs1 = $null
$gs2 = $null
$serverProcesses = @{}

function Stop-SmokeProcesses {
    foreach ($client in $clients) {
        try { $client.Stream.Dispose() } catch { }
        try { $client.Tcp.Close() } catch { }
    }

    foreach ($proc in @($gs1, $gs2, $gateway)) {
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

    $script:gs1 = Start-Process -FilePath 'dotnet' -ArgumentList @($gameServerDll, 'stage6-A-smoke-gs1', '127.0.0.1', $GameServerPort) -WorkingDirectory $gs1Work -PassThru -WindowStyle Hidden -RedirectStandardOutput $gs1Out -RedirectStandardError $gs1Err
    $script:gs2 = Start-Process -FilePath 'dotnet' -ArgumentList @($gameServerDll, 'stage6-A-smoke-gs2', '127.0.0.1', $GameServerPort) -WorkingDirectory $gs2Work -PassThru -WindowStyle Hidden -RedirectStandardOutput $gs2Out -RedirectStandardError $gs2Err

    $script:serverProcesses['stage6-A-smoke-gs1'] = $script:gs1
    $script:serverProcesses['stage6-A-smoke-gs2'] = $script:gs2
    Start-Sleep -Seconds 5
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
    param($Client, [int]$Type, [int]$TimeoutMs = 15000)

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
    Start-Stage6Servers

    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $password = 'stage6pw'
    $hostClient = New-SmokeClient 'host'
    $hostLogin = Register-And-Login $hostClient "s6ahost$suffix" $password

    Send-SmokeMessage $hostClient 'CreateRoom' @{
        playerName = 'Host Recovery'
        sessionId = $hostLogin.SessionId
    }
    $created = Read-UntilType $hostClient 17
    $roomCode = [string]$created.payload.roomCode
    $originalOwner = [string]$created.payload.roomInfo.ownerServerId

    if (-not $serverProcesses.ContainsKey($originalOwner)) {
        throw "Unexpected room owner: $originalOwner"
    }

    Write-Host "Created room $roomCode on $originalOwner"
    Start-Sleep -Seconds 2

    # Stage 6 - A test: stop the owner node and expect Gateway to restore the room elsewhere.
    $ownerProcess = $serverProcesses[$originalOwner]
    Stop-Process -Id $ownerProcess.Id -Force

    $recovered = Read-UntilType $hostClient 28 20000
    $newOwner = [string]$recovered.payload.ownerServerId
    if ([string]::IsNullOrWhiteSpace($newOwner) -or $newOwner -eq $originalOwner) {
        throw "Room was not moved to a new owner. old=$originalOwner new=$newOwner"
    }

    # Stage 6 - A test: joining after recovery proves RoomDirectory points at the restored node.
    $guestClient = New-SmokeClient 'guest'
    $guestLogin = Register-And-Login $guestClient "s6aguest$suffix" $password
    Send-SmokeMessage $guestClient 'Join' @{
        roomCode = $roomCode
        playerName = 'Guest Recovery'
        sessionId = $guestLogin.SessionId
    }

    $joined = Read-UntilType $guestClient 17
    if ([string]$joined.payload.roomCode -ne $roomCode) {
        throw "Guest joined unexpected room $($joined.payload.roomCode)"
    }

    [void](Read-UntilType $hostClient 18)
    Write-Host 'STAGE6_A_SMOKE_PASS'
}
catch {
    Write-Host 'STAGE6_A_SMOKE_FAIL'
    Write-Host $_.Exception.Message
    Write-Host '--- gateway stdout ---'
    if (Test-Path $gatewayOut) { Get-Content $gatewayOut -Tail 260 }
    Write-Host '--- gateway stderr ---'
    if (Test-Path $gatewayErr) { Get-Content $gatewayErr -Tail 260 }
    Write-Host '--- gs1 stdout ---'
    if (Test-Path $gs1Out) { Get-Content $gs1Out -Tail 220 }
    Write-Host '--- gs1 stderr ---'
    if (Test-Path $gs1Err) { Get-Content $gs1Err -Tail 220 }
    Write-Host '--- gs2 stdout ---'
    if (Test-Path $gs2Out) { Get-Content $gs2Out -Tail 220 }
    Write-Host '--- gs2 stderr ---'
    if (Test-Path $gs2Err) { Get-Content $gs2Err -Tail 220 }
    throw
}
finally {
    Stop-SmokeProcesses
    Write-Host "Logs: $tempRoot"
}
