[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'linux-x64', 'osx-arm64')]
    [string] $RuntimeIdentifier,

    [Parameter(Mandatory = $true)]
    [ValidateSet('WindowsHeadless', 'LinuxHeadless', 'MacOsHeadless')]
    [string] $RuntimeProfile,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot,

    [switch] $UseLocalCanDoItAllLibraries,

    [string] $CanDoItAllComponentsExpectedCommit,

    [string] $CanDoItAllFileToolsExpectedCommit,

    [switch] $RetainRuntimeFiles
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputRootPath = [IO.Path]::GetFullPath($OutputRoot)
$pathComparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($outputRootPath.Equals($repositoryRoot, $pathComparison) -or $outputRootPath.StartsWith($repositoryPrefix, $pathComparison)) {
    throw 'Headless validation output must be outside the repository checkout.'
}

$hostContract = if ($IsWindows) {
    [pscustomobject]@{ RuntimeIdentifier = 'win-x64'; Profile = 'WindowsHeadless'; OperatingSystem = 'Windows'; Architecture = 'X64' }
}
elseif ($IsLinux) {
    [pscustomobject]@{ RuntimeIdentifier = 'linux-x64'; Profile = 'LinuxHeadless'; OperatingSystem = 'Linux'; Architecture = 'X64' }
}
elseif ($IsMacOS) {
    [pscustomobject]@{ RuntimeIdentifier = 'osx-arm64'; Profile = 'MacOsHeadless'; OperatingSystem = 'MacOs'; Architecture = 'Arm64' }
}
else {
    throw 'The current operating system is not supported by the core portability headless validation.'
}

if ($RuntimeIdentifier -ne $hostContract.RuntimeIdentifier -or $RuntimeProfile -ne $hostContract.Profile) {
    throw "Host contract mismatch. Expected $($hostContract.RuntimeIdentifier)/$($hostContract.Profile)."
}
if ([Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString() -ne $hostContract.Architecture) {
    throw "Host architecture mismatch. Expected $($hostContract.Architecture)."
}

if (Test-Path -LiteralPath $outputRootPath) {
    if (Get-ChildItem -LiteralPath $outputRootPath -Force | Select-Object -First 1) {
        throw 'Headless validation output root must be absent or empty.'
    }
}
else {
    New-Item -ItemType Directory -Path $outputRootPath | Out-Null
}

$workRootPath = if ($IsWindows) {
    Join-Path ([IO.Path]::GetDirectoryName($outputRootPath)) "cda07-$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
}
else {
    $outputRootPath
}
if ($IsWindows) {
    New-Item -ItemType Directory -Path $workRootPath | Out-Null
}

$publishRoot = Join-Path $workRootPath $(if ($IsWindows) { 'p' } else { 'publish' })
$buildArtifactsRoot = Join-Path $workRootPath $(if ($IsWindows) { 'b' } else { 'build-artifacts' })
$runtimeRoot = Join-Path $workRootPath $(if ($IsWindows) { 'r' } else { 'runtime' })
$publishLogPath = Join-Path $outputRootPath 'publish.log'
$webProjectPath = Join-Path $repositoryRoot 'src\App\CanDoItAll.Web\CanDoItAll.Web.csproj'
$playwrightProjectPath = Join-Path $repositoryRoot 'tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj'
$useLocalLibraries = $UseLocalCanDoItAllLibraries.IsPresent.ToString().ToLowerInvariant()
if ($UseLocalCanDoItAllLibraries -and
    ([string]::IsNullOrWhiteSpace($CanDoItAllComponentsExpectedCommit) -or
     [string]::IsNullOrWhiteSpace($CanDoItAllFileToolsExpectedCommit))) {
    throw 'Explicit source mode requires exact Components and FileTools expected commits.'
}

function Get-SanitizedLog {
    param([string] $Value)

    $sanitized = $Value.Replace($workRootPath, '<runtime-root>', $pathComparison)
    $sanitized = $sanitized.Replace($outputRootPath, '<runtime-root>', $pathComparison)
    return $sanitized.Replace($repositoryRoot, '<repository-root>', $pathComparison)
}

$publishArguments = @(
    'publish',
    $webProjectPath,
    '--configuration', 'Release',
    '--runtime', $RuntimeIdentifier,
    '--self-contained', 'false',
    '--artifacts-path', $buildArtifactsRoot,
    '--output', $publishRoot,
    "-p:UseLocalCanDoItAllLibraries=$useLocalLibraries",
    '/m:1',
    '--nologo',
    '--verbosity:minimal'
)
if ($UseLocalCanDoItAllLibraries) {
    $publishArguments += "-p:CanDoItAllComponentsExpectedCommit=$CanDoItAllComponentsExpectedCommit"
    $publishArguments += "-p:CanDoItAllFileToolsExpectedCommit=$CanDoItAllFileToolsExpectedCommit"
}

$publishOutput = @(& dotnet @publishArguments 2>&1)
$sanitizedPublishOutput = Get-SanitizedLog -Value ($publishOutput -join [Environment]::NewLine)
$sanitizedPublishOutput | Set-Content -LiteralPath $publishLogPath -Encoding utf8
if ($LASTEXITCODE -ne 0) {
    [string[]] $tail = $publishOutput | Select-Object -Last 20
    throw "Headless publish failed with exit code $LASTEXITCODE.`n$($tail -join [Environment]::NewLine)"
}

$webDllPath = Join-Path $publishRoot 'CanDoItAll.Web.dll'
if (!(Test-Path -LiteralPath $webDllPath -PathType Leaf)) {
    throw 'Published Web entry assembly is missing.'
}

$workspaceRoot = Join-Path $runtimeRoot 'workspace'
$controlPlaneRoot = Join-Path $runtimeRoot 'control-plane'
$keysRoot = Join-Path $runtimeRoot 'dataprotection-keys'
$stateRoot = Join-Path $runtimeRoot 'state'
$logsRoot = Join-Path $runtimeRoot 'logs'
$temporaryRoot = Join-Path $runtimeRoot 'temporary'
$xdgDataRoot = Join-Path $runtimeRoot 'xdg-data'
$xdgConfigRoot = Join-Path $runtimeRoot 'xdg-config'
$hostBindingId = "a07-ci-$([Guid]::NewGuid().ToString('N'))"

foreach ($path in @(
    $workspaceRoot,
    $controlPlaneRoot,
    $keysRoot,
    $stateRoot,
    $logsRoot,
    $temporaryRoot,
    $xdgDataRoot,
    $xdgConfigRoot
)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

function Get-FreeTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([Net.IPEndPoint] $listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Stop-WebHost {
    param([Diagnostics.Process] $Process)

    if ($Process.HasExited) {
        return 'Exited'
    }

    if (!$IsWindows) {
        & /bin/kill -TERM $Process.Id
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to send SIGTERM to the published Web host.'
        }

        if ($Process.WaitForExit(30000)) {
            return 'SigTerm'
        }
    }
    elseif ($Process.CloseMainWindow() -and $Process.WaitForExit(10000)) {
        return 'CloseMainWindow'
    }

    $Process.Kill($true)
    if (!$Process.WaitForExit(30000)) {
        throw 'Published Web host did not terminate within the validation timeout.'
    }

    return 'ProcessTermination'
}

function Invoke-BrowserSmoke {
    param([Parameter(Mandatory = $true)][string] $BaseUrl)

    $previousBaseUrl = [Environment]::GetEnvironmentVariable('CANDOITALL_PLAYWRIGHT_BASEURL', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('CANDOITALL_PLAYWRIGHT_BASEURL', $BaseUrl, 'Process')
        $arguments = @(
            'test',
            $playwrightProjectPath,
            '--configuration', 'Release',
            '--no-build',
            '--filter', 'Category=UnixPortabilityBrowserSmoke',
            '--logger', 'trx;LogFileName=browser-smoke.trx',
            '--results-directory', $outputRootPath,
            "-p:UseLocalCanDoItAllLibraries=$useLocalLibraries",
            '--nologo',
            '--verbosity:minimal'
        )
        $output = @(& dotnet @arguments 2>&1)
        $exitCode = $LASTEXITCODE
        Get-SanitizedLog -Value ($output -join [Environment]::NewLine) |
            Set-Content -LiteralPath (Join-Path $outputRootPath 'browser-smoke.log') -Encoding utf8
        if ($exitCode -ne 0) {
            [string[]] $tail = $output | Select-Object -Last 20
            throw "Browser smoke failed with exit code $exitCode.`n$($tail -join [Environment]::NewLine)"
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable('CANDOITALL_PLAYWRIGHT_BASEURL', $previousBaseUrl, 'Process')
    }
}

function Invoke-HostCycle {
    param(
        [Parameter(Mandatory = $true)]
        [int] $Cycle
    )

    $port = Get-FreeTcpPort
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'dotnet'
    $startInfo.WorkingDirectory = $publishRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true
    $startInfo.ArgumentList.Add($webDllPath)

    $environment = [ordered]@{
        'ASPNETCORE_ENVIRONMENT' = 'Development'
        'ASPNETCORE_URLS' = "http://127.0.0.1:$port"
        'RuntimeHost__Profile' = $RuntimeProfile
        'SecretVault__UsageProfile' = 'Headless'
        'SecretVault__Provider' = 'Auto'
        'CANDOITALL_HOST_BINDING_ID' = $hostBindingId
        'Storage__WorkspaceRoot' = $workspaceRoot
        'ControlPlane__RootPath' = $controlPlaneRoot
        'ControlPlane__DataProtectionKeysPath' = $keysRoot
        'ControlPlane__StateRootPath' = $stateRoot
        'ControlPlane__LogsRootPath' = $logsRoot
        'ControlPlane__RuntimeTemporaryRootPath' = $temporaryRoot
        'DataProtection__KeyProtection__Provider' = 'UnprotectedDevelopment'
        'Database__Provider' = 'InMemory'
        'Database__ConnectionString' = 'a07-ci-headless'
        'FileTools__DesktopLaunch__Enabled' = 'false'
        'Processes__Runtime__RequirePostgreSqlForAgentAutomation' = 'false'
        'XDG_DATA_HOME' = $xdgDataRoot
        'XDG_CONFIG_HOME' = $xdgConfigRoot
        'XDG_STATE_HOME' = $stateRoot
        'XDG_RUNTIME_DIR' = $temporaryRoot
    }
    foreach ($entry in $environment.GetEnumerator()) {
        $startInfo.Environment[$entry.Key] = $entry.Value
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (!$process.Start()) {
        throw 'Unable to start the published Web host.'
    }

    $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
    $standardErrorTask = $process.StandardError.ReadToEndAsync()
    $health = $null
    $operations = $null
    $failure = $null
    $terminationMode = 'Unknown'
    $browserSmoke = if ($IsWindows -and $Cycle -eq 1) { 'Pending' } elseif ($IsWindows) { 'PassedInCycle1' } else { 'NotRequired' }

    try {
        $deadline = [DateTimeOffset]::UtcNow.AddMinutes(2)
        while ([DateTimeOffset]::UtcNow -lt $deadline) {
            if ($process.HasExited) {
                throw "Published Web host exited before readiness with code $($process.ExitCode)."
            }

            try {
                $health = Invoke-RestMethod -Uri "http://127.0.0.1:$port/health" -TimeoutSec 5
                $operations = Invoke-RestMethod -Uri "http://127.0.0.1:$port/api/runtime/operations" -TimeoutSec 10
                break
            }
            catch {
                Start-Sleep -Milliseconds 500
            }
        }

        if ($null -eq $health -or $null -eq $operations) {
            throw 'Published Web host did not become ready within two minutes.'
        }
        if ([string] $health -ne 'Healthy') {
            throw "Unexpected health response '$health'."
        }
        if ([string] $operations.state -ne 'Ready' -or !$operations.databaseAndMigrationsReady) {
            throw 'Runtime operations did not report Ready with database initialization complete.'
        }
        if ([string] $operations.hostCapabilities.profile -ne $RuntimeProfile) {
            throw 'Runtime operations reported an unexpected host profile.'
        }

        $purposeRoots = @($operations.hostCapabilities.purposeRoots)
        if ($purposeRoots.Count -ne 7 -or @($purposeRoots | Where-Object { [string] $_.state -ne 'Ready' }).Count -ne 0) {
            throw 'Runtime operations did not report seven Ready purpose roots.'
        }

        $operationsJson = $operations | ConvertTo-Json -Depth 20 -Compress
        if ($operationsJson.Contains($repositoryRoot, $pathComparison) -or $operationsJson.Contains($runtimeRoot, $pathComparison)) {
            throw 'Runtime operations disclosed a repository or physical purpose-root path.'
        }
        if ($IsWindows -and $Cycle -eq 1) {
            Invoke-BrowserSmoke -BaseUrl "http://127.0.0.1:$port"
            $browserSmoke = 'Passed'
        }
    }
    catch {
        $failure = $_
    }
    finally {
        try {
            $terminationMode = Stop-WebHost -Process $process
        }
        catch {
            if ($null -eq $failure) {
                $failure = $_
            }
        }
    }

    $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
    $standardError = $standardErrorTask.GetAwaiter().GetResult()
    $combinedOutput = "$standardOutput`n$standardError"
    Get-SanitizedLog -Value $standardOutput | Set-Content -LiteralPath (Join-Path $outputRootPath "startup-$Cycle.out.log") -Encoding utf8
    Get-SanitizedLog -Value $standardError | Set-Content -LiteralPath (Join-Path $outputRootPath "startup-$Cycle.err.log") -Encoding utf8

    if ($combinedOutput -match '(?im)Unhandled exception|Hosting failed|^\s*(fail|crit):') {
        throw "Published Web host cycle $Cycle emitted a fatal log entry."
    }
    if ($null -ne $failure) {
        throw $failure
    }

    return [pscustomobject]@{
        Cycle = $Cycle
        Health = [string] $health
        OperationsState = [string] $operations.state
        DatabaseAndMigrationsReady = [bool] $operations.databaseAndMigrationsReady
        PurposeRootCount = @($operations.hostCapabilities.purposeRoots).Count
        Profile = [string] $operations.hostCapabilities.profile
        BrowserSmoke = $browserSmoke
        TerminationMode = $terminationMode
        ExitCode = $process.ExitCode
    }
}

$cycles = @(
    Invoke-HostCycle -Cycle 1
    Invoke-HostCycle -Cycle 2
)
$installedRuntimes = @(& dotnet --list-runtimes | ForEach-Object { ($_ -split ' \[')[0] })
$summary = [ordered]@{
    SchemaVersion = 1
    RuntimeIdentifier = $RuntimeIdentifier
    RuntimeProfile = $RuntimeProfile
    OperatingSystem = $hostContract.OperatingSystem
    Architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    ValidationFrameworkDescription = [Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    InstalledRuntimes = $installedRuntimes
    PublishMode = 'FrameworkDependent'
    PublishEntrySha256 = (Get-FileHash -LiteralPath $webDllPath -Algorithm SHA256).Hash.ToLowerInvariant()
    PublishOutsideRepository = $true
    MutableRootsOutsideRepository = $true
    SecretValuesCaptured = $false
    PhysicalPurposeRootsCaptured = $false
    Cycles = $cycles
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $outputRootPath 'headless-summary.json') -Encoding utf8

if (!$RetainRuntimeFiles) {
    $cleanupPaths = if ($workRootPath.Equals($outputRootPath, $pathComparison)) {
        @($buildArtifactsRoot, $publishRoot, $runtimeRoot)
    }
    else {
        @($workRootPath)
    }
    foreach ($path in $cleanupPaths) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

Write-Host "Headless portability validation passed for $RuntimeIdentifier/$RuntimeProfile with $($cycles.Count) restart cycles."
