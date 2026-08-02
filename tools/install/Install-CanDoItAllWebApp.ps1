[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RepoRoot = "",
    [string]$Configuration = "Release",
    [string]$InstallRoot = "",
    [string]$ShortcutPath = "",
    [string]$RuntimeIdentifier = "",
    [string]$BindHost = "127.0.0.1",
    [ValidateRange(1, 65535)]
    [int]$Port = 38473,
    [switch]$SkipDatabaseSetup,
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

function Assert-PathHasNoReparsePoints {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $currentPath = [System.IO.Path]::GetFullPath($PathValue)
    while (-not [string]::IsNullOrWhiteSpace($currentPath)) {
        $item = Get-Item -LiteralPath $currentPath -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description traverses reparse point '$($item.FullName)'. Choose a direct filesystem path."
        }

        $parentPath = Split-Path -Parent $currentPath
        if ([string]::IsNullOrWhiteSpace($parentPath) -or
            [string]::Equals($parentPath, $currentPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        $currentPath = $parentPath
    }
}

function Assert-DirectoryTreeHasNoReparsePoints {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Assert-PathHasNoReparsePoints -PathValue $PathValue -Description $Description
    if (-not (Test-Path -LiteralPath $PathValue -PathType Container)) {
        return
    }

    $pendingDirectories = New-Object System.Collections.Generic.Stack[string]
    $pendingDirectories.Push([System.IO.Path]::GetFullPath($PathValue))
    while ($pendingDirectories.Count -gt 0) {
        $currentDirectory = $pendingDirectories.Pop()
        foreach ($child in @(Get-ChildItem -LiteralPath $currentDirectory -Force -ErrorAction Stop)) {
            if (($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "$Description contains reparse point '$($child.FullName)'. Refusing recursive removal."
            }

            if ($child.PSIsContainer) {
                $pendingDirectories.Push($child.FullName)
            }
        }
    }
}

function Resolve-ValidatedInstallRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        throw "InstallRoot cannot be empty."
    }

    if ([System.Management.Automation.WildcardPattern]::ContainsWildcardCharacters($PathValue)) {
        throw "InstallRoot must not contain wildcard characters."
    }

    try {
        $resolvedInstallRoot = [System.IO.Path]::GetFullPath($PathValue)
        $resolvedRepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
    }
    catch {
        throw "InstallRoot '$PathValue' is not a valid filesystem path."
    }

    [char[]]$trimCharacters = @(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $pathRoot = [System.IO.Path]::GetPathRoot($resolvedInstallRoot)
    $normalizedInstallRoot = $resolvedInstallRoot.TrimEnd($trimCharacters)
    $normalizedPathRoot = $pathRoot.TrimEnd($trimCharacters)
    if ([string]::IsNullOrWhiteSpace($pathRoot) -or
        [string]::Equals(
            $normalizedInstallRoot,
            $normalizedPathRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "InstallRoot must name a directory below a filesystem root."
    }

    if (Test-Path -LiteralPath $resolvedInstallRoot -PathType Leaf) {
        throw "InstallRoot '$resolvedInstallRoot' is an existing file."
    }

    Assert-PathHasNoReparsePoints -PathValue $resolvedInstallRoot -Description "InstallRoot"

    $normalizedRepositoryRoot = $resolvedRepositoryRoot.TrimEnd($trimCharacters)
    $separator = [string][System.IO.Path]::DirectorySeparatorChar
    $installPrefix = $normalizedInstallRoot + $separator
    $repositoryPrefix = $normalizedRepositoryRoot + $separator
    $pathsOverlap = [string]::Equals(
            $normalizedInstallRoot,
            $normalizedRepositoryRoot,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalizedInstallRoot.StartsWith(
            $repositoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalizedRepositoryRoot.StartsWith(
            $installPrefix,
            [System.StringComparison]::OrdinalIgnoreCase)
    if ($pathsOverlap) {
        throw "InstallRoot '$resolvedInstallRoot' must not be the repository root, an ancestor of it, or a directory beneath it."
    }

    return $normalizedInstallRoot
}

function Resolve-ValidatedBindHost {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "BindHost cannot be empty."
    }

    $candidate = $Value
    foreach ($characterCode in @(0, 10, 13, 34, 36, 39, 59, 96)) {
        if ($candidate.IndexOf([char]$characterCode) -ge 0) {
            throw "BindHost contains a character that is not permitted in a generated launcher."
        }
    }

    if (-not [string]::Equals($candidate, $candidate.Trim(), [System.StringComparison]::Ordinal)) {
        throw "BindHost must not contain leading or trailing whitespace."
    }

    $ipAddress = $null
    if ([System.Net.IPAddress]::TryParse($candidate, [ref]$ipAddress)) {
        $canonicalHost = $ipAddress.ToString()
        $urlHost = if ($ipAddress.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) {
            "[$canonicalHost]"
        }
        else {
            $canonicalHost
        }

        return [pscustomobject]@{
            Host = $canonicalHost
            UrlHost = $urlHost
        }
    }

    if ($candidate.Length -gt 253) {
        throw "BindHost must be a valid IP address or an ASCII DNS hostname no longer than 253 characters."
    }

    if (-not [string]::Equals($candidate, "localhost", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "DNS BindHost values are limited to 'localhost'; use a literal local IP address for another interface."
    }
    $candidate = "localhost"

    $labels = @($candidate.Split([char]'.'))
    foreach ($label in $labels) {
        if ($label.Length -lt 1 -or
            $label.Length -gt 63 -or
            $label -notmatch '^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$') {
            throw "BindHost must be a valid IP address or ASCII DNS hostname."
        }
    }

    return [pscustomobject]@{
        Host = $candidate
        UrlHost = $candidate
    }
}

function ConvertTo-PowerShellSingleQuotedLiteral {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return "'" + $Value.Replace("'", "''") + "'"
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

    Assert-DirectoryTreeHasNoReparsePoints -PathValue $Path -Description "Recursive removal target"

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

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    if (-not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
        throw "Directory '$SourceDirectory' was not found."
    }

    $sourceRoot = Resolve-AbsolutePath $SourceDirectory
    $separator = [string][System.IO.Path]::DirectorySeparatorChar
    if (-not $sourceRoot.EndsWith($separator, [System.StringComparison]::Ordinal)) {
        $sourceRoot += $separator
    }

    New-Item -ItemType Directory -Force -Path $DestinationDirectory | Out-Null

    Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File -Force | ForEach-Object {
        $relativePath = $_.FullName.Substring($sourceRoot.Length)
        $destinationPath = Join-Path $DestinationDirectory $relativePath
        $destinationParent = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Force -Path $destinationParent | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force
    }
}

function Assert-RequiredTemplatePacks {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TemplatesRoot
    )

    $requiredFiles = @(
        "Agents\manifest.json",
        "Processes\manifest.json",
        "Workflows\manifest.yaml"
    )

    foreach ($requiredFile in $requiredFiles) {
        $path = Join-Path $TemplatesRoot $requiredFile
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required template pack file '$path' was not found."
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

function Invoke-LauncherWithMutexHandoff {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PowerShellPath,
        [Parameter(Mandatory = $true)]
        [string]$LauncherPath,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory = $true)]
        [System.Threading.Mutex]$InstallMutex,
        [Parameter(Mandatory = $true)]
        [ref]$MutexAcquired,
        [Parameter(Mandatory = $true)]
        [ref]$MigrationMayHaveStarted,
        [switch]$NoBrowser
    )

    if (-not [bool]$MutexAcquired.Value) {
        throw "The installer mutex must be owned before handing it to the installed launcher."
    }

    $launcherHandoffEvent = $null
    $launcherProcess = $null
    $launcherHandoffReleased = $false
    try {
        $launcherHandoffEventName = "Local\CanDoItAll.WebApp.LauncherReady.$([Guid]::NewGuid().ToString('N'))"
        $launcherHandoffEvent = New-Object System.Threading.EventWaitHandle(
            $false,
            [System.Threading.EventResetMode]::ManualReset,
            $launcherHandoffEventName)
        $launcherArgumentList = New-Object System.Collections.Generic.List[string]
        foreach ($argument in @(
            "-NoProfile",
            "-MTA",
            "-ExecutionPolicy",
            "Bypass",
            "-WindowStyle",
            "Hidden",
            "-File",
            $LauncherPath,
            "-InstallerHandoff",
            "-InstallerHandoffEvent",
            $launcherHandoffEventName
        )) {
            $launcherArgumentList.Add($argument)
        }
        if ($NoBrowser.IsPresent) {
            $launcherArgumentList.Add("-NoBrowser")
        }

        $launcherArguments = Format-ShortcutArguments -Arguments $launcherArgumentList.ToArray()
        $launcherProcess = Start-Process `
            -FilePath $PowerShellPath `
            -ArgumentList $launcherArguments `
            -WorkingDirectory $WorkingDirectory `
            -WindowStyle Hidden `
            -PassThru

        $handoffDeadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
        $launcherIsWaiting = $false
        while ([DateTimeOffset]::UtcNow -lt $handoffDeadline) {
            if ($launcherHandoffEvent.WaitOne(250)) {
                $launcherIsWaiting = $true
                break
            }

            $launcherProcess.Refresh()
            if ($launcherProcess.HasExited) {
                throw "The installed launcher exited before accepting the installer handoff. Exit code: $($launcherProcess.ExitCode)."
            }
        }
        if (-not $launcherIsWaiting) {
            throw "The installed launcher did not accept the installer handoff within 30 seconds."
        }

        # SignalAndWait makes the child an atomic mutex waiter before this release,
        # so a third installer cannot enter during the handoff.
        $MigrationMayHaveStarted.Value = $true
        $InstallMutex.ReleaseMutex()
        $MutexAcquired.Value = $false
        $launcherHandoffReleased = $true

        $launcherProcess.WaitForExit()
        if ($launcherProcess.ExitCode -ne 0) {
            throw "The installed launcher failed with exit code $($launcherProcess.ExitCode). Check the installed app logs and launcher error message."
        }
    }
    finally {
        if (-not $launcherHandoffReleased -and
            $null -ne $launcherProcess -and
            -not $launcherProcess.HasExited) {
            Stop-Process -Id $launcherProcess.Id -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $launcherHandoffEvent) {
            $launcherHandoffEvent.Dispose()
        }
    }
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

    return ($stoppedByPid -or $matchingProcesses.Count -gt 0)
}

function Restore-FileFromRollback {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,
        [Parameter(Mandatory = $true)]
        [string]$BackupPath,
        [Parameter(Mandatory = $true)]
        [bool]$OriginallyExisted
    )

    if (-not $OriginallyExisted) {
        if (Test-Path -LiteralPath $DestinationPath -PathType Container) {
            throw "Rollback destination '$DestinationPath' is an unexpected directory."
        }
        Remove-Item -LiteralPath $DestinationPath -Force -ErrorAction SilentlyContinue
        return
    }

    if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) {
        throw "Rollback backup '$BackupPath' is missing."
    }

    $destinationParent = Split-Path -Parent $DestinationPath
    New-Item -ItemType Directory -Force -Path $destinationParent | Out-Null
    Copy-Item -LiteralPath $BackupPath -Destination $DestinationPath -Force
}

function Get-LauncherScriptContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BindHost,
        [Parameter(Mandatory = $true)]
        [string]$BindUrlHost,
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    $template = @'
[CmdletBinding()]
param(
    [switch]$NoBrowser,
    [switch]$Stop,
    [switch]$InstallerHandoff,
    [string]$InstallerHandoffEvent = ""
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
$databaseRoot = Join-Path $runtimeRoot "database"
$databaseManifestPath = Join-Path $databaseRoot "database-manifest.json"
$pidFilePath = Join-Path $runtimeRoot "server.pid"
$stdoutLogPath = Join-Path $logRoot "stdout.log"
$stderrLogPath = Join-Path $logRoot "stderr.log"
$bindUrl = __BIND_URL_LITERAL__
$expectedListenerAddress = __LISTENER_ADDRESS_LITERAL__
$appPort = __APP_PORT__
$launchUrl = __LAUNCH_URL_LITERAL__
$healthUrl = "$launchUrl/health"
$startupTimeoutSeconds = 180

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

function Resolve-InstalledPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    $rootPath = [System.IO.Path]::GetFullPath($Root)
    $candidatePath = if ([System.IO.Path]::IsPathRooted($PathValue)) {
        [System.IO.Path]::GetFullPath($PathValue)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $rootPath $PathValue))
    }

    $separator = [string][System.IO.Path]::DirectorySeparatorChar
    $rootPrefix = $rootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + $separator
    if (-not [string]::Equals($candidatePath, $rootPath, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $candidatePath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Installed database path '$PathValue' resolves outside '$rootPath'."
    }

    return $candidatePath
}

function Read-ProtectedSecret {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Installed database secret was not found at '$Path'. Re-run the database installation script."
    }

    $protectedValue = (Get-Content -LiteralPath $Path -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($protectedValue)) {
        throw "Installed database secret '$Path' is empty. Re-run the database installation script."
    }

    $secureValue = ConvertTo-SecureString -String $protectedValue
    $valuePointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($valuePointer)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($valuePointer)
    }
}

function Invoke-CheckedExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $output = & $FilePath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $details = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
        throw "$Description failed with exit code $LASTEXITCODE.$([Environment]::NewLine)$details"
    }

    return $output
}

function Wait-DockerDatabaseReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DockerPath,
        [Parameter(Mandatory = $true)]
        [string]$ContainerName,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$Username
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        & $DockerPath exec $ContainerName pg_isready -U $Username -d $DatabaseName *> $null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        $stateOutput = @(& $DockerPath container inspect `
            --format "{{.State.Running}}|{{.State.Status}}|{{.State.ExitCode}}" `
            $ContainerName 2>&1)
        $stateExitCode = $LASTEXITCODE
        if ($stateExitCode -ne 0) {
            $stateDetails = ($stateOutput | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
            throw "Installed Docker database '$ContainerName' could not be inspected while waiting for readiness.$([Environment]::NewLine)$stateDetails"
        }

        $state = (($stateOutput | ForEach-Object { [string]$_ }) -join "").Trim()
        if (-not $state.StartsWith("true|", [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Installed Docker database '$ContainerName' exited before it became ready (state: $state). Inspect it with 'docker logs $ContainerName'."
        }

        Start-Sleep -Seconds 1
    }

    throw "Installed Docker database '$ContainerName' did not become ready within 90 seconds."
}

function Start-DockerDatabase {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest
    )

    $dockerCommands = @(Get-Command docker -CommandType Application -All -ErrorAction SilentlyContinue)
    $dockerCommand = $dockerCommands |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.Source) -and [string]$_.Source -match '(?i)\.exe$' } |
        Select-Object -First 1
    if ($null -eq $dockerCommand) {
        $dockerCommand = $dockerCommands | Select-Object -First 1
    }

    if ($null -eq $dockerCommand) {
        throw "The installed database uses Docker, but docker.exe is not available. Start Docker or re-run the database installation script."
    }

    $dockerPath = [string]$dockerCommand.Source
    if ([string]::IsNullOrWhiteSpace($dockerPath)) {
        throw "The installed database uses Docker, but the selected Docker application has no executable path."
    }

    $containerName = [string]$Manifest.docker.containerName
    $expectedVolume = [string]$Manifest.docker.volumeName
    $expectedImage = "postgres:16.14-alpine@sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777"
    if ($containerName -ne "candoitall-webapp-db" -or
        $expectedVolume -ne "candoitall-webapp-db-data" -or
        [string]$Manifest.docker.image -ne $expectedImage) {
        throw "Installed database manifest does not name the dedicated Docker database resources."
    }

    try {
        $inspectOutput = @(Invoke-CheckedExternalCommand `
            -FilePath $dockerPath `
            -Arguments @("container", "inspect", $containerName) `
            -Description "Inspecting installed Docker database '$containerName'")
        $inspectItems = @((($inspectOutput | ForEach-Object { [string]$_ }) -join [Environment]::NewLine) | ConvertFrom-Json)
    }
    catch {
        throw "Installed Docker database container '$containerName' could not be validated. Re-run the database installation script. $($_.Exception.Message)"
    }

    if ($inspectItems.Count -ne 1) {
        throw "Installed Docker database inspection returned an unexpected result. Re-run the database installation script."
    }

    $inspect = $inspectItems[0]
    $labels = $inspect.Config.Labels
    $ownerProperty = if ($null -eq $labels) { $null } else { $labels.PSObject.Properties["com.candoitall.owner"] }
    $schemaProperty = if ($null -eq $labels) { $null } else { $labels.PSObject.Properties["com.candoitall.database-schema"] }
    $roleProperty = if ($null -eq $labels) { $null } else { $labels.PSObject.Properties["com.candoitall.database-role"] }
    if ($null -eq $ownerProperty -or
        [string]$ownerProperty.Value -ne "webapp-install" -or
        $null -eq $schemaProperty -or
        [string]$schemaProperty.Value -ne "1" -or
        $null -eq $roleProperty -or
        [string]$roleProperty.Value -ne "stable" -or
        [string]$inspect.Config.Image -ne [string]$Manifest.docker.image) {
        throw "Docker container '$containerName' is not the database managed by this CanDoItAll installation."
    }

    $dataMounts = @($inspect.Mounts | Where-Object {
        [string]$_.Type -eq "volume" -and
        [string]$_.Name -eq $expectedVolume -and
        [string]$_.Destination -eq "/var/lib/postgresql/data" -and
        [bool]$_.RW
    })
    $portBindings = $inspect.HostConfig.PortBindings
    $portProperty = if ($null -eq $portBindings) { $null } else { $portBindings.PSObject.Properties["5432/tcp"] }
    $bindings = @(if ($null -eq $portProperty) { @() } else { @($portProperty.Value) })
    if ($dataMounts.Count -ne 1 -or
        $bindings.Count -ne 1 -or
        [string]$bindings[0].HostIp -ne [string]$Manifest.host -or
        [string]$bindings[0].HostPort -ne [string]$Manifest.port) {
        throw "Docker container '$containerName' does not match the installed database volume or loopback port. Re-run the database installation script."
    }

    if ([int64]$inspect.HostConfig.Memory -ne [int64](1GB) -or
        [int64]$inspect.HostConfig.NanoCpus -ne [int64]1000000000 -or
        [int64]$inspect.HostConfig.PidsLimit -ne [int64]256 -or
        [int]$inspect.Config.StopTimeout -ne 60) {
        throw "Docker container '$containerName' does not match the installed database resource and shutdown limits. Re-run the database installation script."
    }

    $restartPolicy = $inspect.HostConfig.RestartPolicy
    $logConfig = $inspect.HostConfig.LogConfig
    $logOptions = if ($null -eq $logConfig) { $null } else { $logConfig.Config }
    $logMaxSizeProperty = if ($null -eq $logOptions) { $null } else { $logOptions.PSObject.Properties["max-size"] }
    $logMaxFileProperty = if ($null -eq $logOptions) { $null } else { $logOptions.PSObject.Properties["max-file"] }
    if ($null -eq $restartPolicy -or
        [string]$restartPolicy.Name -ne "unless-stopped" -or
        $null -eq $logConfig -or
        [string]$logConfig.Type -ne "local" -or
        $null -eq $logMaxSizeProperty -or
        [string]$logMaxSizeProperty.Value -ne "10m" -or
        $null -eq $logMaxFileProperty -or
        [string]$logMaxFileProperty.Value -ne "3") {
        throw "Docker container '$containerName' does not match the installed database restart or bounded logging policy. Re-run the database installation script."
    }

    if (-not [bool]$inspect.State.Running) {
        Invoke-CheckedExternalCommand `
            -FilePath $dockerPath `
            -Arguments @("start", $containerName) `
            -Description "Starting installed Docker database '$containerName'" | Out-Null
    }

    Wait-DockerDatabaseReady `
        -DockerPath $dockerPath `
        -ContainerName $containerName `
        -DatabaseName ([string]$Manifest.databaseName) `
        -Username ([string]$Manifest.appUsername)
}

function Wait-NativeDatabaseReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PgIsReadyPath,
        [Parameter(Mandatory = $true)]
        [string]$HostName,
        [Parameter(Mandatory = $true)]
        [int]$DatabasePort,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$Username
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(90)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        & $PgIsReadyPath -h $HostName -p $DatabasePort -U $Username -d $DatabaseName *> $null
        if ($LASTEXITCODE -eq 0) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "Installed native PostgreSQL database did not become ready within 90 seconds."
}

function Rotate-NativePostgreSqlLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        return
    }

    $log = Get-Item -LiteralPath $LogPath
    if ($log.Length -lt 10MB) {
        return
    }

    $archivePath = "$LogPath.1"
    Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue
    Move-Item -LiteralPath $LogPath -Destination $archivePath
}

function Start-NativeDatabase {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest
    )

    if ([string]$Manifest.native.binPath -ne "native\pgsql\bin" -or
        [string]$Manifest.native.dataPath -ne "native\data" -or
        [string]$Manifest.native.logPath -ne "native\logs\postgresql.log") {
        throw "Installed native database manifest does not use the managed PostgreSQL paths. Re-run the database installation script."
    }

    $binPath = Resolve-InstalledPath -Root $databaseRoot -PathValue ([string]$Manifest.native.binPath)
    $dataPath = Resolve-InstalledPath -Root $databaseRoot -PathValue ([string]$Manifest.native.dataPath)
    $logPath = Resolve-InstalledPath -Root $databaseRoot -PathValue ([string]$Manifest.native.logPath)
    $pgCtlPath = Join-Path $binPath "pg_ctl.exe"
    $pgIsReadyPath = Join-Path $binPath "pg_isready.exe"

    foreach ($requiredPath in @($pgCtlPath, $pgIsReadyPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Installed PostgreSQL executable was not found at '$requiredPath'. Re-run the database installation script."
        }
    }

    $pgVersionPath = Join-Path $dataPath "PG_VERSION"
    if (-not (Test-Path -LiteralPath $pgVersionPath -PathType Leaf) -or
        (Get-Content -LiteralPath $pgVersionPath -Raw).Trim() -ne "16") {
        throw "Installed native PostgreSQL data is missing or is not major version 16. Re-run the database installation script."
    }

    & $pgCtlPath status -D $dataPath *> $null
    $statusExitCode = $LASTEXITCODE
    if ($statusExitCode -eq 3) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $logPath) | Out-Null
        Rotate-NativePostgreSqlLog -LogPath $logPath
        Invoke-CheckedExternalCommand `
            -FilePath $pgCtlPath `
            -Arguments @("start", "-D", $dataPath, "-l", $logPath, "-w", "-t", "60") `
            -Description "Starting installed native PostgreSQL database" | Out-Null
    }
    elseif ($statusExitCode -ne 0) {
        throw "pg_ctl could not determine the installed native PostgreSQL status (exit code $statusExitCode)."
    }

    Wait-NativeDatabaseReady `
        -PgIsReadyPath $pgIsReadyPath `
        -HostName ([string]$Manifest.host) `
        -DatabasePort ([int]$Manifest.port) `
        -DatabaseName ([string]$Manifest.databaseName) `
        -Username ([string]$Manifest.appUsername)
}

function Format-ConnectionStringValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return '"' + $Value.Replace('"', '""') + '"'
}

function Initialize-InstalledDatabaseEnvironment {
    if (-not (Test-Path -LiteralPath $databaseManifestPath -PathType Leaf)) {
        throw "The installed database is not configured. Re-run the CanDoItAll web app installer or tools\install\Install-CanDoItAllWebAppDatabase.ps1."
    }

    try {
        $manifest = Get-Content -LiteralPath $databaseManifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Installed database manifest '$databaseManifestPath' is invalid. Re-run the database installation script."
    }

    if ([int]$manifest.schemaVersion -ne 1) {
        throw "Installed database manifest schema '$($manifest.schemaVersion)' is not supported. Re-run the database installation script."
    }

    $hostName = [string]$manifest.host
    $databaseName = [string]$manifest.databaseName
    $username = [string]$manifest.appUsername
    $databasePort = [int]$manifest.port
    if ($hostName -ne "127.0.0.1" -or
        $databasePort -lt 1 -or
        $databasePort -gt 65535 -or
        $databaseName -notmatch '^[a-z][a-z0-9_]{0,62}$' -or
        $username -notmatch '^[a-z][a-z0-9_]{0,62}$') {
        throw "Installed database manifest contains invalid connection metadata. Re-run the database installation script."
    }

    if ([string]$manifest.appPasswordFile -ne "secrets\app-password.dpapi") {
        throw "Installed database manifest does not use the managed application credential path. Re-run the database installation script."
    }

    $appPasswordPath = Resolve-InstalledPath -Root $databaseRoot -PathValue ([string]$manifest.appPasswordFile)
    $appPassword = Read-ProtectedSecret -Path $appPasswordPath

    switch (([string]$manifest.engine).ToLowerInvariant()) {
        "docker" { Start-DockerDatabase -Manifest $manifest }
        "native" { Start-NativeDatabase -Manifest $manifest }
        default { throw "Installed database engine '$($manifest.engine)' is not supported." }
    }

    $env:Database__Provider = "PostgreSql"
    $env:Database__ConnectionString = @(
        "Host=$(Format-ConnectionStringValue -Value $hostName)"
        "Port=$databasePort"
        "Database=$(Format-ConnectionStringValue -Value $databaseName)"
        "Username=$(Format-ConnectionStringValue -Value $username)"
        "Password=$(Format-ConnectionStringValue -Value $appPassword)"
        "Include Error Detail=false"
        "Timeout=15"
        "Command Timeout=30"
    ) -join ";"
}

function Test-Health {
    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2
        return [int]$response.StatusCode -eq 200 -and [string]$response.Content -eq "Healthy"
    }
    catch {
        return $false
    }
}

function Test-ProcessOwnsListener {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process
    )

    try {
        $listeners = @(Get-NetTCPConnection `
            -State Listen `
            -LocalPort $appPort `
            -ErrorAction Stop |
            Where-Object {
                [int]$_.OwningProcess -eq $Process.Id -and
                [string]::Equals(
                    [string]$_.LocalAddress,
                    $expectedListenerAddress,
                    [System.StringComparison]::OrdinalIgnoreCase)
            })
    }
    catch {
        throw "Windows could not verify which process owns the configured web port $appPort. $($_.Exception.Message)"
    }

    return $listeners.Count -gt 0
}

function Get-LogTail {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return ""
    }

    try {
        return (Get-Content -LiteralPath $Path -Tail 40 -ErrorAction Stop) -join [Environment]::NewLine
    }
    catch {
        return ""
    }
}

function Get-StartupFailureMessage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Summary
    )

    $stderrTail = Get-LogTail -Path $stderrLogPath
    if (-not [string]::IsNullOrWhiteSpace($stderrTail)) {
        return "$Summary`n`nRecent stderr:`n$stderrTail"
    }

    $stdoutTail = Get-LogTail -Path $stdoutLogPath
    if (-not [string]::IsNullOrWhiteSpace($stdoutTail)) {
        return "$Summary`n`nRecent log output:`n$stdoutTail"
    }

    return $Summary
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

$launcherMutex = $null
$launcherMutexAcquired = $false
$launcherReadyEvent = $null
try {
    $launcherMutex = New-Object System.Threading.Mutex($false, "Global\CanDoItAll.WebApp.Install.v1")
    try {
        if ($InstallerHandoff.IsPresent) {
            if ($InstallerHandoffEvent -notmatch '^Local\\CanDoItAll\.WebApp\.LauncherReady\.[a-f0-9]{32}$') {
                throw "The internal installer handoff event name is invalid."
            }

            $launcherReadyEvent = [System.Threading.EventWaitHandle]::OpenExisting($InstallerHandoffEvent)
            $launcherMutexAcquired = [System.Threading.WaitHandle]::SignalAndWait(
                $launcherReadyEvent,
                $launcherMutex,
                30000,
                $false)
        }
        else {
            $launcherMutexAcquired = $launcherMutex.WaitOne(0)
        }
    }
    catch [System.Threading.AbandonedMutexException] {
        $launcherMutexAcquired = $true
    }
    if (-not $launcherMutexAcquired) {
        throw "A CanDoItAll web app installation is in progress. Wait for it to finish and launch the app again."
    }

    if (-not (Test-Path -LiteralPath $appPath)) {
        throw "CanDoItAll is not installed at $appPath. Re-run the CanDoItAll web app installer."
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

    Initialize-InstalledDatabaseEnvironment

    $isHealthy = Test-Health
    $runningProcess = Get-InstalledProcess
    if ($isHealthy -and
        ($null -eq $runningProcess -or -not (Test-ProcessOwnsListener -Process $runningProcess))) {
        throw "The configured address '$launchUrl' is already healthy, but it is not owned by this CanDoItAll installation. Stop the other service or choose another BindHost/Port."
    }

    $startedProcess = $runningProcess
    if (-not $isHealthy) {
        if ($null -ne $runningProcess) {
            Stop-Process -Id $runningProcess.Id -Force -ErrorAction Stop
            Remove-Item -LiteralPath $pidFilePath -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
        }

        $env:ASPNETCORE_ENVIRONMENT = "Production"
        $env:DOTNET_ENVIRONMENT = "Production"
        $env:ASPNETCORE_URLS = $bindUrl
        $env:FileTools__DesktopLaunch__Enabled = "__DESKTOP_LAUNCH_ENABLED__"
        $env:ControlPlane__RootPath = $controlPlaneRoot
        $env:Storage__WorkspaceRoot = $workspaceRoot
        $env:Storage__ManagerArtifactsFolder = $managerArtifactsRoot

        $startedProcess = Start-Process `
            -FilePath $appPath `
            -WorkingDirectory $appRoot `
            -WindowStyle Hidden `
            -PassThru `
            -RedirectStandardOutput $stdoutLogPath `
            -RedirectStandardError $stderrLogPath

        Set-Content -LiteralPath $pidFilePath -Value $startedProcess.Id
    }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($startupTimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-Health) {
            $ownedProcess = Get-InstalledProcess
            if ($null -eq $ownedProcess -or -not (Test-ProcessOwnsListener -Process $ownedProcess)) {
                throw "The configured address '$launchUrl' became healthy without an owned CanDoItAll process. Stop the other service or choose another BindHost/Port."
            }

            if (-not $NoBrowser.IsPresent) {
                Start-Process $launchUrl | Out-Null
            }

            exit 0
        }

        if ($null -ne $startedProcess) {
            $startedProcess.Refresh()
            if ($startedProcess.HasExited) {
                throw (Get-StartupFailureMessage -Summary "CanDoItAll exited before it became ready. Exit code: $($startedProcess.ExitCode). Check logs in $logRoot.")
            }
        }

        Start-Sleep -Seconds 1
    }

    throw (Get-StartupFailureMessage -Summary "CanDoItAll did not become ready within $startupTimeoutSeconds seconds. Check logs in $logRoot.")
}
catch {
    Show-LauncherError -Message $_.Exception.Message
    exit 1
}
finally {
    if ($launcherMutexAcquired) {
        $launcherMutex.ReleaseMutex()
    }
    if ($null -ne $launcherMutex) {
        $launcherMutex.Dispose()
    }
    if ($null -ne $launcherReadyEvent) {
        $launcherReadyEvent.Dispose()
    }
}
'@

    $desktopLaunchEnabled = $BindHost -in @("127.0.0.1", "localhost", "::1")
    $bindUrl = "http://${BindUrlHost}:$Port"
    $launchUrlHost = switch ($BindHost) {
        "0.0.0.0" { "127.0.0.1" }
        "::" { "[::1]" }
        "localhost" { "127.0.0.1" }
        default { $BindUrlHost }
    }
    $listenerAddress = if ($BindHost -eq "localhost") { "127.0.0.1" } else { $BindHost }
    $launchUrl = "http://${launchUrlHost}:$Port"
    $content = $template.Replace(
        "__BIND_URL_LITERAL__",
        (ConvertTo-PowerShellSingleQuotedLiteral -Value $bindUrl))
    $content = $content.Replace(
        "__LAUNCH_URL_LITERAL__",
        (ConvertTo-PowerShellSingleQuotedLiteral -Value $launchUrl))
    $content = $content.Replace(
        "__LISTENER_ADDRESS_LITERAL__",
        (ConvertTo-PowerShellSingleQuotedLiteral -Value $listenerAddress))
    $content = $content.Replace("__APP_PORT__", [string]$Port)
    $content = $content.Replace("__DESKTOP_LAUNCH_ENABLED__", $desktopLaunchEnabled.ToString().ToLowerInvariant())
    return $content
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..\..")
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
Assert-PathHasNoReparsePoints -PathValue $RepoRoot -Description "RepoRoot"
$InstallRoot = Resolve-ValidatedInstallRoot -PathValue $InstallRoot -RepositoryRoot $RepoRoot
$bindHostInfo = Resolve-ValidatedBindHost -Value $BindHost
$BindHost = [string]$bindHostInfo.Host
$bindUrlHost = [string]$bindHostInfo.UrlHost
$launchUrlHost = switch ($BindHost) {
    "0.0.0.0" { "127.0.0.1" }
    "::" { "[::1]" }
    "localhost" { "127.0.0.1" }
    default { $bindUrlHost }
}
$ShortcutPath = Resolve-AbsolutePath $ShortcutPath
$projectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "src\App\CanDoItAll.Web\CanDoItAll.Web.csproj")
$sourceTemplatesRoot = Resolve-AbsolutePath (Join-Path $RepoRoot "Templates")
$appRoot = Join-Path $InstallRoot "app"
$runtimeRoot = Join-Path $InstallRoot "runtime"
$pidFilePath = Join-Path $runtimeRoot "server.pid"
$stagingRoot = Join-Path $InstallRoot ".staging"
$publishRoot = Join-Path $stagingRoot ("publish-" + [Guid]::NewGuid().ToString("N"))
$rollbackRoot = Join-Path $stagingRoot ("rollback-" + [Guid]::NewGuid().ToString("N"))
$rollbackAppRoot = Join-Path $rollbackRoot "app"
$rollbackLauncherPath = Join-Path $rollbackRoot "Start-CanDoItAll.ps1"
$rollbackManifestPath = Join-Path $rollbackRoot "install-manifest.json"
$rollbackShortcutPath = Join-Path $rollbackRoot "CanDoItAll.lnk"
$launcherPath = Join-Path $InstallRoot "Start-CanDoItAll.ps1"
$appExePath = Join-Path $appRoot "CanDoItAll.Web.exe"
$manifestPath = Join-Path $InstallRoot "install-manifest.json"
$databaseInstallerPath = Join-Path $PSScriptRoot "Install-CanDoItAllWebAppDatabase.ps1"
$databaseManifestPath = Join-Path $runtimeRoot "database\database-manifest.json"
$launchUrl = "http://${launchUrlHost}:$Port"
$shortcutTarget = (Get-Command powershell -CommandType Application).Source

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Could not find CanDoItAll.Web project at '$projectPath'."
}

Assert-RequiredTemplatePacks -TemplatesRoot $sourceTemplatesRoot

if (-not $PSCmdlet.ShouldProcess(
        $InstallRoot,
        "Publish and install CanDoItAll.Web with its dedicated database")) {
    return [pscustomobject]@{
        Status = "Preview"
        InstallRoot = $InstallRoot
        AppRoot = $appRoot
        DatabaseManifestPath = $databaseManifestPath
        ShortcutPath = $ShortcutPath
        LaunchUrl = $launchUrl
    }
}

$installMutex = New-Object System.Threading.Mutex($false, "Global\CanDoItAll.WebApp.Install.v1")
$installMutexAcquired = $false
try {
    try {
        $installMutexAcquired = $installMutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $installMutexAcquired = $true
    }

    if (-not $installMutexAcquired) {
        throw "Another CanDoItAll web app installation is already running. Wait for it to finish and retry."
    }

    foreach ($managedPath in @(
        $InstallRoot,
        $stagingRoot,
        $publishRoot,
        $rollbackRoot,
        $runtimeRoot,
        $pidFilePath,
        $launcherPath,
        $manifestPath
    )) {
        Assert-PathHasNoReparsePoints -PathValue $managedPath -Description "Managed installation path"
    }

    Write-Status "Preparing install folders under $InstallRoot"
    New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $runtimeRoot | Out-Null

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

Write-Status "Copying repository templates"
Copy-DirectoryContents -SourceDirectory $sourceTemplatesRoot -DestinationDirectory (Join-Path $publishRoot "Templates")
Assert-RequiredTemplatePacks -TemplatesRoot (Join-Path $publishRoot "Templates")

if (-not (Test-Path -LiteralPath $databaseInstallerPath -PathType Leaf)) {
    throw "Could not find the installed-web-app database setup script at '$databaseInstallerPath'."
}

if ($SkipDatabaseSetup.IsPresent) {
    if (-not (Test-Path -LiteralPath $databaseManifestPath -PathType Leaf)) {
        throw "Database setup can be skipped only when an existing managed database manifest is present at '$databaseManifestPath'."
    }

    $databasePreview = @(& $databaseInstallerPath -InstallRoot $InstallRoot -WhatIf) |
        Where-Object { [string]$_.Status -eq "Preview" } |
        Select-Object -Last 1
    if ($null -eq $databasePreview -or
        -not (Test-Path -LiteralPath $databasePreview.ProtectedAppPasswordPath -PathType Leaf)) {
        throw "The existing database manifest or current-user protected application credential is invalid. Run database setup without -SkipDatabaseSetup."
    }

    try {
        $protectedDatabasePassword = (Get-Content -LiteralPath $databasePreview.ProtectedAppPasswordPath -Raw).Trim()
        if ([string]::IsNullOrWhiteSpace($protectedDatabasePassword)) {
            throw "Protected application credential is empty."
        }

        ConvertTo-SecureString -String $protectedDatabasePassword | Out-Null
    }
    catch {
        throw "The existing application database credential cannot be decrypted by the current Windows user. Run database setup without -SkipDatabaseSetup."
    }

    Write-Status "Database provisioning was skipped after validating the existing managed manifest and credential"
}
else {

    Write-Status "Installing or validating the dedicated web app database"
    $databaseSetupResult = & $databaseInstallerPath -InstallRoot $InstallRoot
    if ($null -eq $databaseSetupResult -or
        -not (Test-Path -LiteralPath $databaseManifestPath -PathType Leaf)) {
        throw "Database setup did not create the required manifest at '$databaseManifestPath'."
    }
}

$launcherContent = Get-LauncherScriptContent `
    -BindHost $BindHost `
    -BindUrlHost $bindUrlHost `
    -Port $Port

$shortcutArguments = Format-ShortcutArguments -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-WindowStyle",
    "Hidden",
    "-File",
    $launcherPath
)

$manifestContent = @{
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
    databaseManifestPath = $databaseManifestPath
    databaseSetupSkipped = $SkipDatabaseSetup.IsPresent
    bindHost = $BindHost
    port = $Port
    launchUrl = $launchUrl
} | ConvertTo-Json -Depth 5

if (Test-Path -LiteralPath $appRoot -PathType Leaf) {
    throw "Managed app path '$appRoot' is an existing file."
}

$previousAppExisted = Test-Path -LiteralPath $appRoot -PathType Container
if ($previousAppExisted) {
    Assert-DirectoryTreeHasNoReparsePoints -PathValue $appRoot -Description "Previous managed app tree"
}
else {
    Assert-PathHasNoReparsePoints -PathValue $appRoot -Description "Managed app path"
}

foreach ($managedFilePath in @($launcherPath, $manifestPath, $ShortcutPath)) {
    if (Test-Path -LiteralPath $managedFilePath) {
        if (-not (Test-Path -LiteralPath $managedFilePath -PathType Leaf)) {
            throw "Managed file destination '$managedFilePath' is an existing non-file path."
        }

        $managedFile = Get-Item -LiteralPath $managedFilePath -Force
        if (($managedFile.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Managed file destination '$managedFilePath' is a reparse point."
        }
    }
}

New-Item -ItemType Directory -Force -Path $rollbackRoot | Out-Null
$launcherOriginallyExisted = Test-Path -LiteralPath $launcherPath -PathType Leaf
$manifestOriginallyExisted = Test-Path -LiteralPath $manifestPath -PathType Leaf
$shortcutOriginallyExisted = Test-Path -LiteralPath $ShortcutPath -PathType Leaf
if ($launcherOriginallyExisted) {
    Copy-Item -LiteralPath $launcherPath -Destination $rollbackLauncherPath -Force
}
if ($manifestOriginallyExisted) {
    Copy-Item -LiteralPath $manifestPath -Destination $rollbackManifestPath -Force
}
if ($shortcutOriginallyExisted) {
    Copy-Item -LiteralPath $ShortcutPath -Destination $rollbackShortcutPath -Force
}

$previousWasRunning = $false
$oldAppMoved = $false
$newAppMoved = $false
$installSucceeded = $false
$rollbackCompleted = $false
$migrationMayHaveStarted = $false
try {
    if (Test-Path -LiteralPath $appExePath -PathType Leaf) {
        $previousWasRunning = [bool](Stop-InstalledProcesses `
            -ExpectedExecutablePath $appExePath `
            -PidFilePath $pidFilePath)
    }
    else {
        Remove-Item -LiteralPath $pidFilePath -Force -ErrorAction SilentlyContinue
    }

    if ($previousAppExisted) {
        Assert-DirectoryTreeHasNoReparsePoints -PathValue $appRoot -Description "Previous managed app tree"
        Move-Item -LiteralPath $appRoot -Destination $rollbackAppRoot
        $oldAppMoved = $true
    }

    Write-Status "Replacing installed app files"
    Move-Item -LiteralPath $publishRoot -Destination $appRoot
    $newAppMoved = $true
    Assert-RequiredTemplatePacks -TemplatesRoot (Join-Path $appRoot "Templates")

    Set-Content -LiteralPath $launcherPath -Value $launcherContent -Encoding UTF8

    Set-Shortcut `
        -ShortcutPath $ShortcutPath `
        -TargetPath $shortcutTarget `
        -Arguments $shortcutArguments `
        -WorkingDirectory $InstallRoot `
        -IconLocation (Join-Path $appRoot "CanDoItAll.Web.exe")

    Set-Content -LiteralPath $manifestPath -Value $manifestContent -Encoding UTF8

    if ($StartAfterInstall.IsPresent) {
        Write-Status "Launching CanDoItAll"
        Invoke-LauncherWithMutexHandoff `
            -PowerShellPath $shortcutTarget `
            -LauncherPath $launcherPath `
            -WorkingDirectory $InstallRoot `
            -InstallMutex $installMutex `
            -MutexAcquired ([ref]$installMutexAcquired) `
            -MigrationMayHaveStarted ([ref]$migrationMayHaveStarted)
    }

    $installSucceeded = $true
}
catch {
    $installFailure = $_.Exception
    if ($migrationMayHaveStarted) {
        throw "The new app files were installed, but startup failed after database migrations may have begun: $($installFailure.Message) Automatic binary rollback was skipped because it cannot safely roll back database state. The previous app files remain at '$rollbackAppRoot' for manual recovery."
    }

    $rollbackErrors = New-Object System.Collections.Generic.List[string]
    $appRollbackReady = -not $oldAppMoved

    if ($newAppMoved -and (Test-Path -LiteralPath $appRoot -PathType Container)) {
        try {
            if (Test-Path -LiteralPath $appExePath -PathType Leaf) {
                Stop-InstalledProcesses `
                    -ExpectedExecutablePath $appExePath `
                    -PidFilePath $pidFilePath | Out-Null
            }
            Remove-DirectoryRobust -Path $appRoot
        }
        catch {
            $rollbackErrors.Add("Could not remove the failed new app: $($_.Exception.Message)")
        }
    }

    if ($oldAppMoved -and -not (Test-Path -LiteralPath $appRoot)) {
        try {
            Move-Item -LiteralPath $rollbackAppRoot -Destination $appRoot
            $appRollbackReady = $true
        }
        catch {
            $rollbackErrors.Add("Could not restore the previous app: $($_.Exception.Message)")
        }
    }

    foreach ($fileRollback in @(
        [pscustomobject]@{ Destination = $launcherPath; Backup = $rollbackLauncherPath; Existed = $launcherOriginallyExisted },
        [pscustomobject]@{ Destination = $manifestPath; Backup = $rollbackManifestPath; Existed = $manifestOriginallyExisted },
        [pscustomobject]@{ Destination = $ShortcutPath; Backup = $rollbackShortcutPath; Existed = $shortcutOriginallyExisted }
    )) {
        try {
            Restore-FileFromRollback `
                -DestinationPath $fileRollback.Destination `
                -BackupPath $fileRollback.Backup `
                -OriginallyExisted ([bool]$fileRollback.Existed)
        }
        catch {
            $rollbackErrors.Add("Could not restore '$($fileRollback.Destination)': $($_.Exception.Message)")
        }
    }

    if ($previousWasRunning -and $appRollbackReady -and $rollbackErrors.Count -eq 0) {
        try {
            if (-not (Test-Path -LiteralPath $launcherPath -PathType Leaf)) {
                throw "The previous launcher is unavailable."
            }

            $previousLauncherContent = Get-Content -LiteralPath $launcherPath -Raw
            if ($previousLauncherContent.Contains("InstallerHandoffEvent") -and
                $previousLauncherContent.Contains("SignalAndWait")) {
                $rollbackRestartMayHaveStarted = $false
                Invoke-LauncherWithMutexHandoff `
                    -PowerShellPath $shortcutTarget `
                    -LauncherPath $launcherPath `
                    -WorkingDirectory $InstallRoot `
                    -InstallMutex $installMutex `
                    -MutexAcquired ([ref]$installMutexAcquired) `
                    -MigrationMayHaveStarted ([ref]$rollbackRestartMayHaveStarted) `
                    -NoBrowser
            }
            elseif ($previousLauncherContent.Contains("Global\CanDoItAll.WebApp.Install.v1")) {
                throw "The previous app was restored, but its launcher uses an older mutex protocol that cannot be restarted atomically. Start it manually after this installer exits."
            }
            else {
                # Launchers installed before mutex handoff support do not contend for
                # this mutex, so keep ownership until their startup check completes.
                Invoke-CheckedCommand -FilePath $shortcutTarget -Arguments @(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-WindowStyle",
                    "Hidden",
                    "-File",
                    $launcherPath,
                    "-NoBrowser"
                ) -WorkingDirectory $InstallRoot | Out-Null
            }
        }
        catch {
            $rollbackErrors.Add("Could not restart the previous app: $($_.Exception.Message)")
        }
    }

    $rollbackCompleted = $rollbackErrors.Count -eq 0
    if (-not $rollbackCompleted) {
        throw "Installation failed: $($installFailure.Message)$([Environment]::NewLine)Rollback also reported:$([Environment]::NewLine)$($rollbackErrors -join [Environment]::NewLine)"
    }

    throw $installFailure
}
finally {
    if ($installSucceeded -or $rollbackCompleted) {
        Remove-DirectoryRobust -Path $rollbackRoot
    }
}

Write-Status "Install completed."
Write-Status "App folder: $appRoot"
Write-Status "Database manifest: $databaseManifestPath"
Write-Status "Desktop shortcut: $ShortcutPath"
Write-Status "Launch URL: $launchUrl"

[pscustomobject]@{
    Status = "Installed"
    InstallRoot = $InstallRoot
    AppRoot = $appRoot
    DatabaseManifestPath = $databaseManifestPath
    ShortcutPath = $ShortcutPath
    LaunchUrl = $launchUrl
}
}
finally {
    if (Test-Path -LiteralPath $publishRoot) {
        try {
            Remove-DirectoryRobust -Path $publishRoot
        }
        catch {
            Write-Warning "Could not remove the current publish staging directory '$publishRoot': $($_.Exception.Message)"
        }
    }
    if ($installMutexAcquired) {
        $installMutex.ReleaseMutex()
    }
    $installMutex.Dispose()
}
