[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundleRoot = Split-Path -Parent $scriptRoot
$repoRoot = Split-Path -Parent $bundleRoot

$baseSettingsPath = Join-Path $repoRoot "CanDoItAll.Mcp.DotNetWatch.settings.json"
$isolatedSettingsPath = Join-Path $bundleRoot "artifacts\\manual-benchmark-backend.settings.json"
$isolatedRegistrationPath = Join-Path $repoRoot ".mcp-state\\backend\\manual-benchmark-registration.json"
$isolatedLaunchLockPath = Join-Path $repoRoot ".mcp-state\\backend\\manual-benchmark-launch.lock"
$backendDllPath = Join-Path $repoRoot "src\\CanDoItAll.Mcp.DotNetWatch\\bin\\Debug\\net10.0\\CanDoItAll.Mcp.DotNetWatch.dll"
$backendStdOutPath = Join-Path $repoRoot ".mcp-state\\logs\\manual-benchmark-backend.stdout.log"
$backendStdErrPath = Join-Path $repoRoot ".mcp-state\\logs\\manual-benchmark-backend.stderr.log"
$benchmarkScriptPath = Join-Path $bundleRoot "tools\\managed_mcp_watch_benchmark.js"
$pageHeaderConfigPath = Join-Path $bundleRoot "artifacts\\managed-mcp-pageheader-fullflow.config.json"
$projectsConfigPath = Join-Path $bundleRoot "artifacts\\managed-mcp-projects-page-fullflow.config.json"
$summaryPath = Join-Path $bundleRoot "artifacts\\final-watch-benchmark-summary.json"

function Write-Section([string]$message) {
    Write-Host ""
    Write-Host "== $message =="
}

function Stop-ProcessIfRunning([System.Diagnostics.Process]$process) {
    if ($null -eq $process) {
        return
    }

    try {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
        }
    }
    catch {
        Write-Warning "Failed to stop backend process $($process.Id): $($_.Exception.Message)"
    }
}

if (-not $SkipBuild) {
    Write-Section "Build backend"
    & dotnet build $repoRoot\src\CanDoItAll.Mcp.DotNetWatch\CanDoItAll.Mcp.DotNetWatch.csproj -c Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Backend build failed."
    }
}

Write-Section "Prepare isolated backend settings"
$settings = Get-Content $baseSettingsPath -Raw | ConvertFrom-Json
$settings.Process.UsePollingFileWatcher = $false
if ($null -ne $settings.DefaultApp.EnvironmentOverlay.PSObject.Properties["DOTNET_USE_POLLING_FILE_WATCHER"]) {
    $settings.DefaultApp.EnvironmentOverlay.PSObject.Properties.Remove("DOTNET_USE_POLLING_FILE_WATCHER")
}
if ($null -eq $settings.PSObject.Properties["Backend"]) {
    $settings | Add-Member -NotePropertyName Backend -NotePropertyValue ([pscustomobject]@{
        Enabled = $true
        BindHost = "127.0.0.1"
        RegistrationPath = ".mcp-state/backend/manual-benchmark-registration.json"
        LaunchLockPath = ".mcp-state/backend/manual-benchmark-launch.lock"
        StartupTimeoutMs = 30000
        StartupPollIntervalMs = 250
    })
}
else {
    $settings.Backend.RegistrationPath = ".mcp-state/backend/manual-benchmark-registration.json"
    $settings.Backend.LaunchLockPath = ".mcp-state/backend/manual-benchmark-launch.lock"
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $isolatedSettingsPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $isolatedRegistrationPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backendStdOutPath) | Out-Null

$settings | ConvertTo-Json -Depth 100 | Set-Content $isolatedSettingsPath -Encoding UTF8
Remove-Item $isolatedRegistrationPath -Force -ErrorAction SilentlyContinue
Remove-Item $isolatedLaunchLockPath -Force -ErrorAction SilentlyContinue
Remove-Item $backendStdOutPath -Force -ErrorAction SilentlyContinue
Remove-Item $backendStdErrPath -Force -ErrorAction SilentlyContinue

$token = [guid]::NewGuid().ToString("N").ToUpperInvariant()
$backendProcess = $null

try {
    Write-Section "Start isolated backend"
    $backendProcess = Start-Process -FilePath dotnet -ArgumentList @(
        $backendDllPath,
        "--backend",
        "--settings", $isolatedSettingsPath,
        "--backend-token", $token
    ) -WorkingDirectory $repoRoot -RedirectStandardOutput $backendStdOutPath -RedirectStandardError $backendStdErrPath -PassThru

    $deadline = (Get-Date).AddSeconds(30)
    $registration = $null
    do {
        Start-Sleep -Milliseconds 250

        if ($backendProcess.HasExited) {
            throw "Isolated backend exited early with code $($backendProcess.ExitCode)."
        }

        if (Test-Path $isolatedRegistrationPath) {
            $registration = Get-Content $isolatedRegistrationPath -Raw | ConvertFrom-Json
            if ($registration.authToken -eq $token) {
                break
            }
        }
    } while ((Get-Date) -lt $deadline)

    if ($null -eq $registration -or $registration.authToken -ne $token) {
        throw "Timed out waiting for isolated backend registration."
    }

    Write-Host "Isolated backend: $($registration.baseUrl)"
    Write-Host "Backend PID: $($backendProcess.Id)"

    Write-Section "Run page header benchmark"
    & node $benchmarkScriptPath --registration $isolatedRegistrationPath --config $pageHeaderConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "PageHeader benchmark failed."
    }

    Write-Section "Run projects page benchmark"
    & node $benchmarkScriptPath --registration $isolatedRegistrationPath --config $projectsConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "ProjectsPage benchmark failed."
    }

    Write-Section "Summarize results"
    $pageHeader = Get-Content (Join-Path $bundleRoot "artifacts\\managed-mcp-pageheader-fullflow.json") -Raw | ConvertFrom-Json
    $projects = Get-Content (Join-Path $bundleRoot "artifacts\\managed-mcp-projects-page-fullflow.json") -Raw | ConvertFrom-Json

    $summary = [ordered]@{
        generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
        backend = [ordered]@{
            baseUrl = $registration.baseUrl
            processId = $backendProcess.Id
            binaryVersionMarker = $registration.identity.binaryVersionMarker
            settingsPath = $isolatedSettingsPath
            registrationPath = $isolatedRegistrationPath
        }
        pageHeader = [ordered]@{
            startupElapsedMs = $pageHeader.startupElapsedMs
            watchReportedAppliedElapsedMs = $pageHeader.watchReportedAppliedElapsedMs
            revisionConfirmedElapsedMs = $pageHeader.revisionConfirmedElapsedMs
            visibleAfterReloadMs = $pageHeader.visibleAfterReloadMs
            timedOut = $pageHeader.timedOut
        }
        projectsPage = [ordered]@{
            startupElapsedMs = $projects.startupElapsedMs
            watchReportedAppliedElapsedMs = $projects.watchReportedAppliedElapsedMs
            revisionConfirmedElapsedMs = $projects.revisionConfirmedElapsedMs
            visibleAfterReloadMs = $projects.visibleAfterReloadMs
            timedOut = $projects.timedOut
        }
    }

    $summary | ConvertTo-Json -Depth 100 | Set-Content $summaryPath -Encoding UTF8
    Get-Content $summaryPath
}
finally {
    Write-Section "Stop isolated backend"
    Stop-ProcessIfRunning $backendProcess
}
