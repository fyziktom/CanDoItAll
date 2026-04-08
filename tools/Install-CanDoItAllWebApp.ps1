[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$Configuration = "Release",
    [string]$InstallRoot = "",
    [string]$ShortcutPath = "",
    [string]$RuntimeIdentifier = "",
    [string]$BindHost = "127.0.0.1",
    [int]$Port = 38473,
    [switch]$StartAfterInstall
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    return [System.IO.Path]::GetFullPath($PathValue)
}

function Write-Status {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host "[CanDoItAll Web Install] $Message"
}

function Remove-DirectoryRobust {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 6) {
                throw
            }

            Start-Sleep -Milliseconds (250 * $attempt)
        }
    }
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [string]$WorkingDirectory = ""
    )

    if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        & $FilePath @Arguments
    }
    else {
        Push-Location $WorkingDirectory
        try {
            & $FilePath @Arguments
        }
        finally {
            Pop-Location
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Get-DetectedRuntimeIdentifier {
    $architecture = $env:PROCESSOR_ARCHITEW6432
    if ([string]::IsNullOrWhiteSpace($architecture)) {
        $architecture = $env:PROCESSOR_ARCHITECTURE
    }

    if ([string]::IsNullOrWhiteSpace($architecture)) {
        $architecture = if ([Environment]::Is64BitOperatingSystem) { "AMD64" } else { "X86" }
    }

    switch ($architecture.ToUpperInvariant()) {
        "AMD64" { return "win-x64" }
        "X64" { return "win-x64" }
        "ARM64" { return "win-arm64" }
        "X86" {
            if ([Environment]::Is64BitOperatingSystem) {
                return "win-x64"
            }

            return "win-x86"
        }
        default { throw "Unsupported Windows architecture '$architecture'. Specify -RuntimeIdentifier explicitly." }
    }
}

function Format-ShortcutArguments {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return ($Arguments | ForEach-Object {
        if ($_ -match '\s') {
            '"' + $_ + '"'
        }
        else {
            $_
        }
    }) -join ' '
}

function Set-Shortcut {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShortcutPath,
        [Parameter(Mandatory = $true)]
        [string]$TargetPath,
        [Parameter(Mandatory = $true)]
        [string]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory = $true)]
        [string]$IconLocation
    )

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ShortcutPath) | Out-Null

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $null
    try {
        $shortcut = $shell.CreateShortcut($ShortcutPath)
        $shortcut.TargetPath = $TargetPath
        $shortcut.Arguments = $Arguments
        $shortcut.WorkingDirectory = $WorkingDirectory
        $shortcut.IconLocation = $IconLocation
        $shortcut.WindowStyle = 7
        $shortcut.Save()
    }
    finally {
        if ($null -ne $shortcut) {
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shortcut) | Out-Null
        }

        if ($null -ne $shell) {
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
        }
    }
}

function Try-StopProcessById {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PidFilePath,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedExecutablePath
    )

    if (-not (Test-Path -LiteralPath $PidFilePath)) {
        return $false
    }

    try {
        $processIdText = (Get-Content -LiteralPath $PidFilePath -Raw).Trim()
        if ([string]::IsNullOrWhiteSpace($processIdText)) {
            return $false
        }

        $processId = [int]$processIdText
        $process = Get-Process -Id $processId -ErrorAction Stop
        $processPath = $null
        try {
            $processPath = $process.Path
        }
        catch {
            $processPath = $null
        }

        if (-not [string]::IsNullOrWhiteSpace($processPath) -and
            [string]::Equals(
                (Resolve-AbsolutePath $processPath),
                (Resolve-AbsolutePath $ExpectedExecutablePath),
                [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Status "Stopping installed CanDoItAll process $processId"
            Stop-Process -Id $processId -Force -ErrorAction Stop
            return $true
        }
    }
    catch {
        return $false
    }
    finally {
        Remove-Item -LiteralPath $PidFilePath -Force -ErrorAction SilentlyContinue
    }

    return $false
}

function Stop-InstalledProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExpectedExecutablePath,
        [Parameter(Mandatory = $true)]
        [string]$PidFilePath
    )

    $stoppedByPid = Try-StopProcessById -PidFilePath $PidFilePath -ExpectedExecutablePath $ExpectedExecutablePath
    $normalizedExecutablePath = Resolve-AbsolutePath $ExpectedExecutablePath

    $matchingProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'CanDoItAll.Web.exe'" |
        Where-Object {
            $executablePath = [string]$_.ExecutablePath
            $commandLine = [string]$_.CommandLine

            if (-not [string]::IsNullOrWhiteSpace($executablePath)) {
                try {
                    if ([string]::Equals(
                        (Resolve-AbsolutePath $executablePath),
                        $normalizedExecutablePath,
                        [System.StringComparison]::OrdinalIgnoreCase)) {
                        return $true
                    }
                }
                catch {
                }
            }

            return -not [string]::IsNullOrWhiteSpace($commandLine) -and
                $commandLine.IndexOf($normalizedExecutablePath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
        })

    foreach ($process in $matchingProcesses) {
        try {
            Write-Status "Stopping installed CanDoItAll process $($process.ProcessId)"
            Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
        }
        catch {
            Write-Status "Failed to stop process $($process.ProcessId): $($_.Exception.Message)"
        }
    }

    if ($stoppedByPid -or $matchingProcesses.Count -gt 0) {
        Start-Sleep -Seconds 1
    }
}

function Get-LauncherScriptContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BindHost,
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    $template = @'
[CmdletBinding()]
param(
    [switch]$NoBrowser,
    [switch]$Stop
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$installRoot = $PSScriptRoot
$appRoot = Join-Path $installRoot "app"
$appPath = Join-Path $appRoot "CanDoItAll.Web.exe"
$runtimeRoot = Join-Path $installRoot "runtime"
$logRoot = Join-Path $installRoot "logs"
$controlPlaneRoot = Join-Path $runtimeRoot "control-plane"
$workspaceRoot = Join-Path $runtimeRoot "workspace"
$managerArtifactsRoot = Join-Path $runtimeRoot "manager-artifacts"
$pidFilePath = Join-Path $runtimeRoot "server.pid"
$stdoutLogPath = Join-Path $logRoot "stdout.log"
$stderrLogPath = Join-Path $logRoot "stderr.log"
$bindUrl = "http://__BIND_HOST__:__PORT__"
$launchUrl = $bindUrl
$healthUrl = "$bindUrl/health"

function Show-LauncherError {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        [System.Windows.Forms.MessageBox]::Show(
            $Message,
            "CanDoItAll",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
    }
    catch {
        Write-Error $Message
    }
}

function Test-Health {
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2
        return [int]$response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Get-InstalledProcess {
    if (-not (Test-Path -LiteralPath $pidFilePath)) {
        return $null
    }

    try {
        $processIdText = (Get-Content -LiteralPath $pidFilePath -Raw).Trim()
        if ([string]::IsNullOrWhiteSpace($processIdText)) {
            return $null
        }

        $process = Get-Process -Id ([int]$processIdText) -ErrorAction Stop
        $processPath = $null
        try {
            $processPath = $process.Path
        }
        catch {
            $processPath = $null
        }

        if (-not [string]::IsNullOrWhiteSpace($processPath) -and
            [string]::Equals(
                [System.IO.Path]::GetFullPath($processPath),
                [System.IO.Path]::GetFullPath($appPath),
                [System.StringComparison]::OrdinalIgnoreCase)) {
            return $process
        }
    }
    catch {
        return $null
    }

    return $null
}

function Stop-InstalledProcess {
    $process = Get-InstalledProcess
    if ($null -eq $process) {
        Remove-Item -LiteralPath $pidFilePath -Force -ErrorAction SilentlyContinue
        return
    }

    Stop-Process -Id $process.Id -Force -ErrorAction Stop
    Remove-Item -LiteralPath $pidFilePath -Force -ErrorAction SilentlyContinue
}

try {
    if (-not (Test-Path -LiteralPath $appPath)) {
        throw "CanDoItAll is not installed at $appPath. Re-run tools\Install-CanDoItAllWebApp.ps1."
    }

    New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $controlPlaneRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $workspaceRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $managerArtifactsRoot | Out-Null

    if ($Stop.IsPresent) {
        Stop-InstalledProcess
        exit 0
    }

    $isHealthy = Test-Health
    if (-not $isHealthy) {
        $runningProcess = Get-InstalledProcess
        if ($null -ne $runningProcess) {
            Stop-Process -Id $runningProcess.Id -Force -ErrorAction Stop
            Remove-Item -LiteralPath $pidFilePath -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
        }

        $env:ASPNETCORE_ENVIRONMENT = "Production"
        $env:DOTNET_ENVIRONMENT = "Production"
        $env:ASPNETCORE_URLS = $bindUrl
        $env:ControlPlane__RootPath = $controlPlaneRoot
        $env:Storage__WorkspaceRoot = $workspaceRoot
        $env:Storage__ManagerArtifactsFolder = $managerArtifactsRoot

        $process = Start-Process `
            -FilePath $appPath `
            -WorkingDirectory $appRoot `
            -WindowStyle Hidden `
            -PassThru `
            -RedirectStandardOutput $stdoutLogPath `
            -RedirectStandardError $stderrLogPath

        Set-Content -LiteralPath $pidFilePath -Value $process.Id
    }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-Health) {
            if (-not $NoBrowser.IsPresent) {
                Start-Process $launchUrl | Out-Null
            }

            exit 0
        }

        Start-Sleep -Seconds 1
    }

    throw "CanDoItAll did not become ready within 30 seconds. Check logs in $logRoot."
}
catch {
    Show-LauncherError -Message $_.Exception.Message
    exit 1
}
'@

    return $template.Replace("__BIND_HOST__", $BindHost).Replace("__PORT__", [string]$Port)
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..")
}

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $InstallRoot = Join-Path $env:LOCALAPPDATA "CanDoItAll\WebApp"
}

if ([string]::IsNullOrWhiteSpace($ShortcutPath)) {
    $ShortcutPath = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "CanDoItAll.lnk"
}

if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $RuntimeIdentifier = Get-DetectedRuntimeIdentifier
}

$RepoRoot = Resolve-AbsolutePath $RepoRoot
$InstallRoot = Resolve-AbsolutePath $InstallRoot
$ShortcutPath = Resolve-AbsolutePath $ShortcutPath
$projectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "src\CanDoItAll.Web\CanDoItAll.Web.csproj")
$appRoot = Join-Path $InstallRoot "app"
$runtimeRoot = Join-Path $InstallRoot "runtime"
$pidFilePath = Join-Path $runtimeRoot "server.pid"
$stagingRoot = Join-Path $InstallRoot ".staging"
$publishRoot = Join-Path $stagingRoot ("publish-" + [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss"))
$launcherPath = Join-Path $InstallRoot "Start-CanDoItAll.ps1"
$appExePath = Join-Path $appRoot "CanDoItAll.Web.exe"
$manifestPath = Join-Path $InstallRoot "install-manifest.json"
$launchUrl = "http://${BindHost}:$Port"
$shortcutTarget = (Get-Command powershell -CommandType Application).Source

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Could not find CanDoItAll.Web project at '$projectPath'."
}

Write-Status "Preparing install folders under $InstallRoot"
New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null

if (Test-Path -LiteralPath $appExePath) {
    Stop-InstalledProcesses -ExpectedExecutablePath $appExePath -PidFilePath $pidFilePath
}
else {
    Remove-Item -LiteralPath $pidFilePath -Force -ErrorAction SilentlyContinue
}

Remove-DirectoryRobust -Path $publishRoot

Write-Status "Publishing CanDoItAll.Web ($Configuration, $RuntimeIdentifier, self-contained)"
Invoke-CheckedCommand -FilePath "dotnet" -Arguments @(
    "publish",
    $projectPath,
    "-c",
    $Configuration,
    "-r",
    $RuntimeIdentifier,
    "--self-contained",
    "true",
    "-o",
    $publishRoot,
    "-p:UseAppHost=true",
    "-p:PublishSingleFile=false"
) -WorkingDirectory $RepoRoot

$publishedExePath = Join-Path $publishRoot "CanDoItAll.Web.exe"
if (-not (Test-Path -LiteralPath $publishedExePath)) {
    throw "Publish completed, but the Windows app host was not found at '$publishedExePath'."
}

Write-Status "Replacing installed app files"
Remove-DirectoryRobust -Path $appRoot
Move-Item -LiteralPath $publishRoot -Destination $appRoot

$launcherContent = Get-LauncherScriptContent -BindHost $BindHost -Port $Port
Set-Content -LiteralPath $launcherPath -Value $launcherContent -Encoding UTF8

$shortcutArguments = Format-ShortcutArguments -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-WindowStyle",
    "Hidden",
    "-File",
    $launcherPath
)

Set-Shortcut `
    -ShortcutPath $ShortcutPath `
    -TargetPath $shortcutTarget `
    -Arguments $shortcutArguments `
    -WorkingDirectory $InstallRoot `
    -IconLocation (Join-Path $appRoot "CanDoItAll.Web.exe")

$manifest = @{
    updatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    repoRoot = $RepoRoot
    projectPath = $projectPath
    configuration = $Configuration
    runtimeIdentifier = $RuntimeIdentifier
    installRoot = $InstallRoot
    appRoot = $appRoot
    runtimeRoot = $runtimeRoot
    launcherPath = $launcherPath
    shortcutPath = $ShortcutPath
    bindHost = $BindHost
    port = $Port
    launchUrl = $launchUrl
} | ConvertTo-Json -Depth 5

Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

if ($StartAfterInstall.IsPresent) {
    Write-Status "Launching CanDoItAll"
    Invoke-CheckedCommand -FilePath "powershell" -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-WindowStyle",
        "Hidden",
        "-File",
        $launcherPath
    ) -WorkingDirectory $InstallRoot
}

Write-Status "Install completed."
Write-Status "App folder: $appRoot"
Write-Status "Desktop shortcut: $ShortcutPath"
Write-Status "Launch URL: $launchUrl"
