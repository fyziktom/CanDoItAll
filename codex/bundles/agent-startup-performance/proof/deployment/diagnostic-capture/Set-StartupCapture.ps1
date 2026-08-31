[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'Start')]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9-]{5,44}$')][string]$CaptureId,
    [Parameter(Mandatory, ParameterSetName = 'Start')][switch]$Start,
    [Parameter(Mandatory, ParameterSetName = 'Start')][ValidateRange(1, 2147483647)][int]$NativeAppPid,
    [Parameter(Mandatory, ParameterSetName = 'Start')][string]$NativeExpectedStartUtc,
    [Parameter(Mandatory, ParameterSetName = 'Start')][ValidatePattern('^[a-f0-9]{64}$')][string]$ClientId,
    [Parameter(Mandatory, ParameterSetName = 'Start')][ValidateRange(1, 2147483647)][int]$ClientAppPid,
    [Parameter(Mandatory, ParameterSetName = 'Start')][ValidatePattern('^/tmp/dotnet-diagnostic-[0-9]+-[0-9]+-socket$')][string]$ClientDiagnosticSocket,
    [Parameter(Mandatory, ParameterSetName = 'Start')][switch]$RootSamplingGo,
    [Parameter(Mandatory, ParameterSetName = 'Stop')][switch]$Stop
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$diagnosticsRoot = Join-Path $repositoryRoot '.artifacts/agent-startup-performance/diagnostics'
$helperRoot = Join-Path $diagnosticsRoot 'bin/Release/net10.0'
$helperDll = Join-Path $helperRoot 'StartupDispatchCapture.dll'
$helperTar = Join-Path $diagnosticsRoot 'helper.tar'
$proofRoot = Join-Path $repositoryRoot 'codex/bundles/agent-startup-performance/proof'
$captureRoot = Join-Path $proofRoot "SB03/performance/after/$CaptureId"
$ownershipPath = Join-Path $captureRoot 'capture-ownership.json'
$nativeStopFile = Join-Path $diagnosticsRoot "stop-native-$CaptureId.signal"
$guestRoot = "/tmp/agent-startup-diagnostics-$CaptureId"
$clientStopFile = "$guestRoot.stop"
$nativeOutput = Join-Path $captureRoot 'native-http-capture.jsonl'
$clientOutput = Join-Path $captureRoot 'client-http-capture.jsonl'
$nativeError = Join-Path $captureRoot 'native-http-capture.stderr.log'
$clientError = Join-Path $captureRoot 'client-http-capture.stderr.log'
$baseline = Get-Content -LiteralPath (Join-Path $proofRoot 'phase-0/host-preflight.json') -Raw | ConvertFrom-Json -AsHashtable -DateKind String
$image = Get-Content -LiteralPath (Join-Path $proofRoot 'deployment/docker-image/image-result.json') -Raw | ConvertFrom-Json -AsHashtable
$expectedBinaries = @(Get-Content -LiteralPath (Join-Path $proofRoot 'phase-0/diagnostic-helper/binary-sha256.json') -Raw | ConvertFrom-Json)
$dockerPath = (Get-Command docker.exe -CommandType Application -ErrorAction Stop).Source
$dotnetPath = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source

function Invoke-CaptureDocker {
    param([string[]]$Arguments, [string]$InputFile)
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $dockerPath
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.RedirectStandardInput = ![string]::IsNullOrWhiteSpace($InputFile)
    foreach ($argument in $Arguments) {
        $info.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (!$process.Start()) {
            throw 'Diagnostic Docker helper did not start.'
        }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if ($info.RedirectStandardInput) {
            $stream = [IO.File]::OpenRead($InputFile)
            try {
                $copy = $stream.CopyToAsync($process.StandardInput.BaseStream)
                $copy.WaitAsync([TimeSpan]::FromSeconds(45)).GetAwaiter().GetResult()
            } finally {
                $stream.Dispose()
                $process.StandardInput.Close()
            }
        }
        if (!$process.WaitForExit(55000)) {
            throw 'Diagnostic Docker helper exceeded its bound; no application process was signaled.'
        }
        $text = $stdout.GetAwaiter().GetResult()
        [void]$stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw 'Diagnostic Docker helper failed; command payload/configuration omitted.'
        }
        return $text
    } finally {
        $process.Dispose()
    }
}

function Read-CaptureContainer {
    param([string]$Identity, [switch]$ForStop)
    $configuration = @(Invoke-CaptureDocker @('inspect', '--type', 'container', $Identity) | ConvertFrom-Json -AsHashtable -DateKind String)[0]
    if ($configuration.Id -ne $Identity -or $configuration.Name -ne '/candoitall-shared-providers-manual-client-a-1' -or
        $configuration.Image -ne $image.ImageId -or
        (!$ForStop -and (!$configuration.State.Running -or $configuration.State.Health.Status -ne 'healthy')) -or
        $configuration.Config.User -ne '1654:1654' -or !$configuration.HostConfig.ReadonlyRootfs -or
        !$configuration.HostConfig.Tmpfs.ContainsKey('/tmp') -or $Identity -in @($baseline.Containers.Id)) {
        throw 'The exact candidate client identity/security boundary is invalid.'
    }
    $port = @($configuration.HostConfig.PortBindings['8080/tcp'])
    if ($configuration.HostConfig.PortBindings.Count -ne 1 -or $port.Count -ne 1 -or $port[0].HostIp -ne '127.0.0.1' -or $port[0].HostPort -ne '5214') {
        throw 'The candidate diagnostic target does not exclusively publish loopback5214.'
    }
    return $configuration
}

function Read-CaptureRecord {
    param([string]$Path, [string]$Kind)
    if (!(Test-Path -LiteralPath $Path)) {
        return $null
    }
    foreach ($line in @(Get-Content -LiteralPath $Path)) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        try {
            $record = $line | ConvertFrom-Json -AsHashtable -DateKind String
        } catch {
            continue
        }
        if ($record.kind -eq $Kind) {
            return $record
        }
    }
    return $null
}

function Assert-OwnedCaptureProcess {
    param([int]$Identity, [string]$StartedAtUtc)
    $process = Get-Process -Id $Identity -ErrorAction SilentlyContinue
    if ($null -ne $process -and $process.StartTime.ToUniversalTime().ToString('O') -ne $StartedAtUtc) {
        throw 'A recorded collector host PID was reused; no process signal was sent.'
    }
    return $process
}

$endpoint = (Invoke-CaptureDocker @('context', 'inspect', '--format', '{{.Endpoints.docker.Host}}')).Trim()
if ($endpoint -ne 'npipe:////./pipe/dockerDesktopLinuxEngine') {
    throw 'Unexpected Docker endpoint for the diagnostic capture.'
}
if ($PSCmdlet.ParameterSetName -eq 'Stop') {
    $ownership = Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json -AsHashtable -DateKind String
    if ($ownership.CaptureId -ne $CaptureId -or $ownership.NativeStopFile -ne $nativeStopFile -or $ownership.ClientStopFile -ne $clientStopFile) {
        throw 'Capture ownership/stop paths do not match the exact capture ID.'
    }
    $stopTarget = Read-CaptureContainer $ownership.ClientId -ForStop
    [void](Assert-OwnedCaptureProcess $ownership.NativeHostPid $ownership.NativeHostStartedUtc)
    [void](Assert-OwnedCaptureProcess $ownership.DockerExecHostPid $ownership.DockerExecHostStartedUtc)
    if (!$PSCmdlet.ShouldProcess($CaptureId, 'Create only the two owned collector stop files and wait for collector exit')) {
        return
    }
    [IO.File]::WriteAllText($nativeStopFile, 'stop')
    if ($stopTarget.State.Running) {
        [void](Invoke-CaptureDocker @('exec', $ownership.ClientId, '/usr/local/bin/busybox', 'touch', $clientStopFile))
    }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(35)
    do {
        $native = Assert-OwnedCaptureProcess $ownership.NativeHostPid $ownership.NativeHostStartedUtc
        $client = Assert-OwnedCaptureProcess $ownership.DockerExecHostPid $ownership.DockerExecHostStartedUtc
        if ($null -eq $native -and $null -eq $client) {
            break
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    $nativeStopped = Read-CaptureRecord $nativeOutput 'stopped'
    $clientStopped = Read-CaptureRecord $clientOutput 'stopped'
    $nativeStderrBytes = (Get-Item -LiteralPath $nativeError).Length
    $clientStderrBytes = (Get-Item -LiteralPath $clientError).Length
    $protocolClean = $null -ne $nativeStopped -and $null -ne $clientStopped -and
        $nativeStopped.unexpectedArguments -eq 0 -and $clientStopped.unexpectedArguments -eq 0 -and
        $nativeStderrBytes -eq 0 -and $clientStderrBytes -eq 0
    $success = $null -eq $native -and $null -eq $client -and $protocolClean
    [ordered]@{
        StoppedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        CaptureId = $CaptureId
        BothCollectorsExited = $null -eq $native -and $null -eq $client
        Native = $nativeStopped
        Client = $clientStopped
        NativeStderrBytes = $nativeStderrBytes
        ClientStderrBytes = $clientStderrBytes
        ProtocolClean = $protocolClean
        ApplicationProcessesSignaled = $false
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $captureRoot 'capture-stop-verification.json') -Encoding utf8NoBOM
    if (!$success) {
        throw 'Collector stop lacked clean exit/protocol evidence; inspect the sanitized stop report. No application was signaled.'
    }
    Write-Output 'Both owned diagnostic collectors stopped; application processes were not signaled.'
    return
}
if (!$RootSamplingGo) {
    throw 'Root must confirm all builds/tests are finished before candidate sampling.'
}
if (Test-Path -LiteralPath $captureRoot) {
    throw 'Capture proof path already exists; choose a fresh capture ID.'
}
if (Test-Path -LiteralPath $nativeStopFile) {
    throw 'Native stop marker already exists; choose a fresh capture ID.'
}
foreach ($binary in $expectedBinaries) {
    if ((Get-FileHash -LiteralPath (Join-Path $helperRoot $binary.File) -Algorithm SHA256).Hash -ne $binary.Sha256) {
        throw 'A frozen diagnostic helper dependency changed; do not rebuild it.'
    }
}
if ((Get-FileHash -LiteralPath $helperTar -Algorithm SHA256).Hash -ne '9B1968A9C8C60A3C53A56706413772B0F8B50501F085C7C6DCB1343B454862C9') {
    throw 'The frozen diagnostic helper tar archive changed.'
}
$nativeTarget = Get-Process -Id $NativeAppPid -ErrorAction Stop
$candidateNativeRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts/asn-20260831')) + [IO.Path]::DirectorySeparatorChar
if ($NativeAppPid -eq $baseline.Native.Pid -or $nativeTarget.StartTime.ToUniversalTime().ToString('O') -ne $NativeExpectedStartUtc -or
    !$nativeTarget.Path.StartsWith($candidateNativeRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The exact native candidate PID/start/path identity is invalid.'
}
$owners = @(Get-NetTCPConnection -LocalPort 5032 -State Listen | Select-Object -ExpandProperty OwningProcess -Unique)
if ($owners.Count -ne 1 -or $owners[0] -ne $NativeAppPid) {
    throw 'The native candidate does not exclusively own5032.'
}
if (!('Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient' -as [type])) {
    Add-Type -Path (Join-Path $helperRoot 'Microsoft.Diagnostics.NETCore.Client.dll')
}
if (@([Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient]::GetPublishedProcesses()) -notcontains $NativeAppPid) {
    throw 'The exact native diagnostic endpoint is not published.'
}
$clientTarget = Read-CaptureContainer $ClientId
if ($ClientDiagnosticSocket -notmatch "^/tmp/dotnet-diagnostic-$ClientAppPid-[0-9]+-socket$") {
    throw 'Client socket does not identify the supplied target PID.'
}
$preflightCommand = 'test -S "$1" && test -d "/proc/$2" && test "$(/usr/local/bin/busybox cat /proc/$2/comm)" = dotnet && test ! -e "$3" && test ! -e "$4"'
[void](Invoke-CaptureDocker @('exec', $ClientId, '/usr/local/bin/busybox', 'sh', '-c', $preflightCommand, 'capture-preflight', $ClientDiagnosticSocket, [string]$ClientAppPid, $guestRoot, $clientStopFile))
if (!$PSCmdlet.ShouldProcess("nativePID$NativeAppPid and client$ClientId/PID$ClientAppPid", 'Attach the unchanged bounded diagnostic helper; transfer only its verified tar into existing client/tmp')) {
    return
}
[void](New-Item -ItemType Directory -Path $captureRoot)
$transferCommand = 'test -S "$1" && test ! -e "$2" && /usr/local/bin/busybox mkdir "$2" && /usr/local/bin/busybox tar -xf - -C "$2"'
[void](Invoke-CaptureDocker -Arguments @('exec', '-i', $ClientId, '/usr/local/bin/busybox', 'timeout', '45', '/usr/local/bin/busybox', 'sh', '-c', $transferCommand, 'capture-transfer', $ClientDiagnosticSocket, $guestRoot) -InputFile $helperTar)
$hashArguments = @('exec', $ClientId, '/usr/local/bin/busybox', 'sha256sum') + @($expectedBinaries | ForEach-Object { "$guestRoot/$($_.File)" })
$guestHashes = (Invoke-CaptureDocker $hashArguments) -split "`n" | Where-Object { ![string]::IsNullOrWhiteSpace($_) }
if (@($guestHashes).Count -ne $expectedBinaries.Count) {
    throw 'The transferred helper dependency inventory differs.'
}
foreach ($binary in $expectedBinaries) {
    $expectedLine = $binary.Sha256.ToLowerInvariant() + '  ' + $guestRoot + '/' + $binary.File
    if (@($guestHashes | ForEach-Object { $_.TrimEnd("`r") }) -notcontains $expectedLine) {
        throw 'A transferred helper dependency differs from the baseline hash.'
    }
}
$nativeArguments = '"' + $helperDll + '" --pid ' + $NativeAppPid + ' --seconds 1800 --stop-file "' + $nativeStopFile + '"'
$clientArguments = "exec $ClientId dotnet $guestRoot/StartupDispatchCapture.dll --pid $ClientAppPid --seconds 1800 --stop-file $clientStopFile"
$nativeCapture = $null
$clientCapture = $null
try {
    $nativeCapture = Start-Process -FilePath $dotnetPath -ArgumentList $nativeArguments -WorkingDirectory $repositoryRoot -WindowStyle Hidden -RedirectStandardOutput $nativeOutput -RedirectStandardError $nativeError -PassThru
    $clientCapture = Start-Process -FilePath $dockerPath -ArgumentList $clientArguments -WorkingDirectory $repositoryRoot -WindowStyle Hidden -RedirectStandardOutput $clientOutput -RedirectStandardError $clientError -PassThru
    $ownership = [ordered]@{
        CaptureId = $CaptureId
        StartedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        NativeTargetPid = $NativeAppPid
        NativeTargetStartedUtc = $NativeExpectedStartUtc
        NativeHostPid = $nativeCapture.Id
        NativeHostStartedUtc = $nativeCapture.StartTime.ToUniversalTime().ToString('O')
        DockerExecHostPid = $clientCapture.Id
        DockerExecHostStartedUtc = $clientCapture.StartTime.ToUniversalTime().ToString('O')
        ClientId = $ClientId
        ClientImageId = $clientTarget.Image
        ClientStartedUtc = $clientTarget.State.StartedAt
        ClientTargetPid = $ClientAppPid
        ClientDiagnosticSocket = $ClientDiagnosticSocket
        HelperSha256 = 'AFBC63722CB8D696EE3E667009254C1F8ABBB9A593C56E28433760F415D96952'
        HelperTarSha256 = '9B1968A9C8C60A3C53A56706413772B0F8B50501F085C7C6DCB1343B454862C9'
        MaximumSeconds = 1800
        NativeStopFile = $nativeStopFile
        ClientStopFile = $clientStopFile
    }
    $ownership | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ownershipPath -Encoding utf8NoBOM
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    do {
        $nativeReady = Read-CaptureRecord $nativeOutput 'ready'
        $clientReady = Read-CaptureRecord $clientOutput 'ready'
        if ($null -ne $nativeReady -and $null -ne $clientReady) {
            break
        }
        if ($nativeCapture.HasExited -or $clientCapture.HasExited) {
            throw 'A collector exited before both ready records arrived.'
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    if ($nativeReady.targetPid -ne $NativeAppPid -or $nativeReady.capturePid -ne $nativeCapture.Id -or
        $clientReady.targetPid -ne $ClientAppPid -or $clientReady.capturePid -eq $ClientAppPid -or
        $nativeReady.filterVersion -ne 1 -or $clientReady.filterVersion -ne 1 -or
        $nativeReady.maximumSeconds -ne 1800 -or $clientReady.maximumSeconds -ne 1800 -or
        $nativeReady.rawTracePersisted -ne $false -or $clientReady.rawTracePersisted -ne $false) {
        throw 'Both ready records did not match the frozen protocol and exact targets.'
    }
    $nativeCurrent = Get-Process -Id $NativeAppPid -ErrorAction Stop
    $clientCurrent = Read-CaptureContainer $ClientId
    if ($nativeCurrent.StartTime.ToUniversalTime().ToString('O') -ne $NativeExpectedStartUtc -or
        $clientCurrent.State.StartedAt -ne $clientTarget.State.StartedAt) {
        throw 'An application target restarted during collector setup.'
    }
    [ordered]@{
        Native = $nativeReady
        Client = $clientReady
        BothReady = $true
        RootMustGateUiAfterThisRecord = $true
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $captureRoot 'capture-ready.json') -Encoding utf8NoBOM
    Write-Output "Both unchanged diagnostic collectors are ready. Capture=$CaptureId. UI sends may begin only after root accepts this record."
} catch {
    $nativeStopRequested = $false
    $clientStopRequested = $false
    try {
        [IO.File]::WriteAllText($nativeStopFile, 'stop')
        $nativeStopRequested = $true
    } catch {
        $nativeStopRequested = $false
    }
    try {
        [void](Invoke-CaptureDocker @('exec', $ClientId, '/usr/local/bin/busybox', 'touch', $clientStopFile))
        $clientStopRequested = $true
    } catch {
        $clientStopRequested = $false
    }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(35)
    do {
        $nativeExited = $null -eq $nativeCapture -or $nativeCapture.HasExited
        $clientExited = $null -eq $clientCapture -or $clientCapture.HasExited
        if ($nativeExited -and $clientExited) {
            break
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    [ordered]@{
        FailedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        CaptureId = $CaptureId
        NativeStopRequested = $nativeStopRequested
        ClientStopRequested = $clientStopRequested
        NativeCollectorExited = $nativeExited
        ClientCollectorExited = $clientExited
        NativeHostPid = $nativeCapture.Id
        DockerExecHostPid = $clientCapture.Id
        ApplicationProcessesSignaled = $false
        StopMarkersRetained = $true
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $captureRoot 'capture-start-failure.json') -Encoding utf8NoBOM
    throw 'Capture startup failed; only owned stop markers were requested. Review sanitized collector logs/failure report; no application process was signaled.'
}
