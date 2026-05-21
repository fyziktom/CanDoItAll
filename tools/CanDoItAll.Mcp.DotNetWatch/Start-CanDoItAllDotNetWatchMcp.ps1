[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$ProjectPath = "",
    [string]$SettingsPath = "",
    [string]$ShadowArtifactsPath = "",
    [string]$Configuration = "Release",
    [switch]$ForceRebuild,
    [switch]$PrepareOnly
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    return [System.IO.Path]::GetFullPath($PathValue)
}

function Write-Bootstrap {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $timestamp = [DateTimeOffset]::UtcNow.ToString("O")
    $line = "$timestamp $Message"
    [Console]::Error.WriteLine($line)
    Add-Content -Path $script:BootstrapLogPath -Value $line
}

function Get-TrackedFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$CandidatePaths
    )

    $trackedFiles = New-Object System.Collections.Generic.List[System.IO.FileInfo]

    foreach ($candidatePath in $CandidatePaths) {
        if (-not (Test-Path -LiteralPath $candidatePath)) {
            continue
        }

        $item = Get-Item -LiteralPath $candidatePath
        if ($item.PSIsContainer) {
            $files = Get-ChildItem -LiteralPath $candidatePath -Recurse -File -Include *.cs, *.csproj, *.props, *.targets, *.json
            foreach ($file in $files) {
                $trackedFiles.Add($file)
            }
        }
        else {
            $trackedFiles.Add($item)
        }
    }

    return $trackedFiles
}

function Get-SourceSignature {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$TrackedFiles
    )

    $rows = $TrackedFiles |
        Sort-Object FullName |
        ForEach-Object { "{0}|{1}|{2}" -f $_.FullName, $_.Length, $_.LastWriteTimeUtc.Ticks }

    $payload = [System.Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($payload)
        return ([System.BitConverter]::ToString($hash) -replace "-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-ShadowBuildRootName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Signature,
        [Parameter(Mandatory = $true)]
        [bool]$IncludeTimestamp
    )

    $prefixLength = [Math]::Min(20, $Signature.Length)
    $name = $Signature.Substring(0, $prefixLength)
    if ($IncludeTimestamp) {
        $name = "{0}-{1}" -f $name, ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
    }

    return $name
}

function Get-ShadowManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath
    )

    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Get-MsBuildProperty {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$PropertyName,
        [Parameter(Mandatory = $true)]
        [string]$Configuration
    )

    $propertyArgument = "-getProperty:$PropertyName"
    $output = & dotnet msbuild $ProjectPath -nologo $propertyArgument "-p:Configuration=$Configuration" "-p:UseAppHost=false" "-p:CopyRepositoryTemplatesToOutput=false" 2>&1
    foreach ($line in $output) {
        $text = $line.ToString()
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            Add-Content -Path $script:BootstrapLogPath -Value ("{0} msbuild-property | {1}" -f [DateTimeOffset]::UtcNow.ToString("O"), $text)
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to resolve MSBuild property '$PropertyName' for '$ProjectPath' with exit code $LASTEXITCODE."
    }

    $value = ($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ToString()) } | Select-Object -Last 1).ToString().Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "MSBuild property '$PropertyName' resolved to an empty value for '$ProjectPath'."
    }

    return Resolve-AbsolutePath $value
}

function Write-ShadowManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$Signature,
        [Parameter(Mandatory = $true)]
        [string]$BuildRoot,
        [Parameter(Mandatory = $true)]
        [string]$ShadowDllPath
    )

    $payload = @{
        signature = $Signature
        buildRoot = $BuildRoot
        shadowDllPath = $ShadowDllPath
        updatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    } | ConvertTo-Json

    Set-Content -LiteralPath $ManifestPath -Value $payload
}

function Write-ShadowFailureManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$Signature,
        [Parameter(Mandatory = $true)]
        [string]$BuildRoot,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    $payload = @{
        signature = $Signature
        buildRoot = $BuildRoot
        failedUtc = [DateTimeOffset]::UtcNow.ToString("O")
        failureMessage = $FailureMessage
    } | ConvertTo-Json

    Set-Content -LiteralPath $ManifestPath -Value $payload
}

function Get-ShadowRetentionCount {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SettingsPath
    )

    if (-not (Test-Path -LiteralPath $SettingsPath)) {
        return 2
    }

    try {
        $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
        $configuredValue = [int]$settings.ShadowHost.RetainedBuildCount
        if ($configuredValue -gt 0) {
            return $configuredValue
        }
    }
    catch {
        Write-Bootstrap "shadow cleanup | settings parse failed | error=$($_.Exception.Message)"
    }

    return 2
}

function Get-LiveShadowBuildRoots {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.DirectoryInfo[]]$BuildDirectories
    )

    $liveRoots = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    if ($BuildDirectories.Count -eq 0) {
        return $liveRoots
    }

    try {
        $processes = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'"
        foreach ($process in $processes) {
            $commandLine = [string]$process.CommandLine
            if ([string]::IsNullOrWhiteSpace($commandLine)) {
                continue
            }

            foreach ($directory in $BuildDirectories) {
                if ($commandLine.IndexOf($directory.FullName, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    [void]$liveRoots.Add($directory.FullName)
                }
            }
        }
    }
    catch {
        Write-Bootstrap "shadow cleanup | live-root-scan failed | error=$($_.Exception.Message)"
    }

    return $liveRoots
}

function Remove-DirectoryRobust {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $attemptMessages = New-Object System.Collections.Generic.List[string]

    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue |
                ForEach-Object {
                    try {
                        $_.Attributes = [System.IO.FileAttributes]::Normal
                    }
                    catch {
                    }
                }

            [System.IO.Directory]::Delete($Path, $true)
        }
        catch [System.IO.DirectoryNotFoundException] {
            return
        }
        catch {
            [void]$attemptMessages.Add("attempt=$attempt directory-delete error=$($_.Exception.Message)")
        }

        if (-not (Test-Path -LiteralPath $Path)) {
            return
        }

        $escapedPath = $Path.Replace('"', '""')
        & cmd.exe /d /c "rmdir /s /q ""$escapedPath""" | Out-Null
        $commandExitCode = $LASTEXITCODE

        if (-not (Test-Path -LiteralPath $Path)) {
            return
        }

        [void]$attemptMessages.Add("attempt=$attempt cmd-exit=$commandExitCode")
        Start-Sleep -Milliseconds (250 * $attempt)
    }

    throw "Robust delete failed for '$Path'. Path still exists after retries. Details=$($attemptMessages -join '; ')"
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,
        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory,
        [string[]]$ExcludedTopLevelNames = @()
    )

    if (-not (Test-Path -LiteralPath $SourceDirectory)) {
        throw "Source directory '$SourceDirectory' does not exist."
    }

    Remove-DirectoryRobust -Path $DestinationDirectory
    New-Item -ItemType Directory -Force -Path $DestinationDirectory | Out-Null

    $excludedNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in $ExcludedTopLevelNames) {
        [void]$excludedNames.Add($name)
    }

    foreach ($item in Get-ChildItem -LiteralPath $SourceDirectory -Force) {
        if ($excludedNames.Contains($item.Name)) {
            continue
        }

        Copy-Item -LiteralPath $item.FullName -Destination $DestinationDirectory -Recurse -Force
    }
}

function Move-BuildRootToRetired {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$RetiredBuildsRoot
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    New-Item -ItemType Directory -Force -Path $RetiredBuildsRoot | Out-Null
    $retiredName = "{0}-retired-{1}" -f ([System.IO.Path]::GetFileName($Path)), ([Guid]::NewGuid().ToString("N"))
    $retiredPath = Join-Path $RetiredBuildsRoot $retiredName
    Move-Item -LiteralPath $Path -Destination $retiredPath -Force
    return $retiredPath
}

function Invoke-ShadowCleanup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BuildsRoot,
        [Parameter(Mandatory = $true)]
        [string]$RetiredBuildsRoot,
        [Parameter(Mandatory = $true)]
        [string]$CurrentManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$PreviousManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$FailedManifestPath,
        [Parameter(Mandatory = $true)]
        [int]$RetainedBuildCount
    )

    if (-not (Test-Path -LiteralPath $BuildsRoot)) {
        return
    }

    $buildDirectories = @(Get-ChildItem -LiteralPath $BuildsRoot -Directory | Sort-Object LastWriteTimeUtc -Descending)
    if ($buildDirectories.Count -eq 0) {
        return
    }

    $protectedSuccessfulRoots = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($manifestPath in @($CurrentManifestPath, $PreviousManifestPath)) {
        $manifest = Get-ShadowManifest -ManifestPath $manifestPath
        if ($null -ne $manifest -and -not [string]::IsNullOrWhiteSpace($manifest.buildRoot) -and (Test-Path -LiteralPath $manifest.buildRoot)) {
            [void]$protectedSuccessfulRoots.Add((Resolve-AbsolutePath $manifest.buildRoot))
        }
    }

    foreach ($directory in $buildDirectories) {
        if ($protectedSuccessfulRoots.Count -ge $RetainedBuildCount) {
            break
        }

        [void]$protectedSuccessfulRoots.Add($directory.FullName)
    }

    $protectedRoots = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($root in $protectedSuccessfulRoots) {
        [void]$protectedRoots.Add($root)
    }

    $failedManifest = Get-ShadowManifest -ManifestPath $FailedManifestPath
    if ($null -ne $failedManifest -and -not [string]::IsNullOrWhiteSpace($failedManifest.buildRoot) -and (Test-Path -LiteralPath $failedManifest.buildRoot)) {
        [void]$protectedRoots.Add((Resolve-AbsolutePath $failedManifest.buildRoot))
    }

    $liveRoots = Get-LiveShadowBuildRoots -BuildDirectories $buildDirectories
    foreach ($root in $liveRoots) {
        if ($protectedRoots.Add($root)) {
            Write-Bootstrap "shadow cleanup | preserving live build root | buildRoot=$root"
        }
    }

    foreach ($directory in $buildDirectories) {
        if ($protectedRoots.Contains($directory.FullName)) {
            continue
        }

        try {
            $retiredPath = Move-BuildRootToRetired -Path $directory.FullName -RetiredBuildsRoot $RetiredBuildsRoot
            if ($null -ne $retiredPath) {
                Remove-DirectoryRobust -Path $retiredPath
                Write-Bootstrap "shadow cleanup | removed buildRoot=$($directory.FullName)"
                continue
            }

            Write-Bootstrap "shadow cleanup | buildRoot already gone | buildRoot=$($directory.FullName)"
        }
        catch {
            Write-Bootstrap "shadow cleanup | skipped buildRoot=$($directory.FullName) | error=$($_.Exception.Message)"
        }
    }

    if (-not (Test-Path -LiteralPath $RetiredBuildsRoot)) {
        return
    }

    foreach ($directory in @(Get-ChildItem -LiteralPath $RetiredBuildsRoot -Directory -ErrorAction SilentlyContinue)) {
        try {
            Remove-DirectoryRobust -Path $directory.FullName
        }
        catch {
            Write-Bootstrap "shadow cleanup | retired-build-root pending | buildRoot=$($directory.FullName) | error=$($_.Exception.Message)"
        }
    }
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..\..")
}

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $RepoRoot "src\CanDoItAll.Mcp.DotNetWatch\CanDoItAll.Mcp.DotNetWatch.csproj"
}

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path $RepoRoot "CanDoItAll.Mcp.DotNetWatch.settings.json"
}

if ([string]::IsNullOrWhiteSpace($ShadowArtifactsPath)) {
    $ShadowArtifactsPath = Join-Path $RepoRoot ".artifacts\mcp-server-shadow"
}

$RepoRoot = Resolve-AbsolutePath $RepoRoot
$ProjectPath = Resolve-AbsolutePath $ProjectPath
$SettingsPath = Resolve-AbsolutePath $SettingsPath
$ShadowArtifactsPath = Resolve-AbsolutePath $ShadowArtifactsPath

$logDirectory = Join-Path $RepoRoot ".mcp-state\logs"
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$script:BootstrapLogPath = Join-Path $logDirectory "mcp-dotnetwatch-bootstrap.log"

$sourceRoots =
@(
    (Join-Path $RepoRoot "src\CanDoItAll.Mcp.DotNetWatch"),
    (Join-Path $RepoRoot "src\CanDoItAll.Mcp.Core"),
    (Join-Path $RepoRoot "src\CanDoItAll.Mcp.LocalRuntime"),
    (Join-Path $RepoRoot "Directory.Build.props"),
    (Join-Path $RepoRoot "Directory.Build.targets"),
    (Join-Path $RepoRoot "Directory.Packages.props"),
    (Join-Path $RepoRoot "global.json")
)

$trackedFiles = @(Get-TrackedFiles -CandidatePaths $sourceRoots)
$sourceSignature = Get-SourceSignature -TrackedFiles $trackedFiles
$buildsRoot = Join-Path $ShadowArtifactsPath "builds"
$retiredBuildsRoot = Join-Path $ShadowArtifactsPath "retired-builds"
$manifestPath = Join-Path $ShadowArtifactsPath "current.json"
$previousManifestPath = Join-Path $ShadowArtifactsPath "previous.json"
$failedManifestPath = Join-Path $ShadowArtifactsPath "last-failed.json"
$retainedBuildCount = Get-ShadowRetentionCount -SettingsPath $SettingsPath
$configurationSegment = $Configuration.ToLowerInvariant()

New-Item -ItemType Directory -Force -Path $buildsRoot | Out-Null
New-Item -ItemType Directory -Force -Path $retiredBuildsRoot | Out-Null

Write-Bootstrap "wrapper start | repo=$RepoRoot | project=$ProjectPath | settings=$SettingsPath | shadow=$ShadowArtifactsPath | signature=$sourceSignature"

$manifest = Get-ShadowManifest -ManifestPath $manifestPath
$shadowNeedsRefresh = $ForceRebuild.IsPresent

if (-not $shadowNeedsRefresh) {
    if ($null -eq $manifest) {
        $shadowNeedsRefresh = $true
        Write-Bootstrap "shadow check | manifest missing"
    }
    elseif ($manifest.signature -ne $sourceSignature) {
        $shadowNeedsRefresh = $true
        Write-Bootstrap "shadow check | signature changed | current=$($manifest.signature) | next=$sourceSignature"
    }
    elseif ([string]::IsNullOrWhiteSpace($manifest.buildRoot) -or -not (Test-Path -LiteralPath $manifest.buildRoot)) {
        $shadowNeedsRefresh = $true
        Write-Bootstrap "shadow check | manifest build root missing | path=$($manifest.buildRoot)"
    }
    elseif (-not (Test-Path -LiteralPath $manifest.shadowDllPath)) {
        $shadowNeedsRefresh = $true
        Write-Bootstrap "shadow check | manifest dll missing | path=$($manifest.shadowDllPath)"
    }
}

if ($shadowNeedsRefresh) {
    $buildRootName = Get-ShadowBuildRootName -Signature $sourceSignature -IncludeTimestamp $ForceRebuild.IsPresent
    $buildRoot = Join-Path $buildsRoot $buildRootName
    $shadowOutputPath = Join-Path $buildRoot "app"
    $shadowDllPath = Join-Path $shadowOutputPath "CanDoItAll.Mcp.DotNetWatch.dll"

    if (-not (Test-Path -LiteralPath $shadowDllPath)) {
        Write-Bootstrap "shadow build start | buildRoot=$buildRoot"
        try {
            $targetDirectory = Get-MsBuildProperty -ProjectPath $ProjectPath -PropertyName "TargetDir" -Configuration $Configuration
            $buildOutput = & dotnet build $ProjectPath -c $Configuration -p:UseAppHost=false -p:CopyRepositoryTemplatesToOutput=false 2>&1
            foreach ($line in $buildOutput) {
                $text = $line.ToString()
                [Console]::Error.WriteLine($text)
                Add-Content -Path $script:BootstrapLogPath -Value ("{0} build | {1}" -f [DateTimeOffset]::UtcNow.ToString("O"), $text)
            }

            if ($LASTEXITCODE -ne 0) {
                throw "Shadow build failed with exit code $LASTEXITCODE. See $script:BootstrapLogPath."
            }

            Write-Bootstrap "shadow copy start | source=$targetDirectory | target=$shadowOutputPath"
            Copy-DirectoryContents -SourceDirectory $targetDirectory -DestinationDirectory $shadowOutputPath -ExcludedTopLevelNames @("Templates")

            if (-not (Test-Path -LiteralPath $shadowDllPath)) {
                throw "Shadow build completed without producing '$shadowDllPath'."
            }

            Write-Bootstrap "shadow build completed | buildRoot=$buildRoot"
        }
        catch {
            Write-ShadowFailureManifest -ManifestPath $failedManifestPath -Signature $sourceSignature -BuildRoot $buildRoot -FailureMessage $_.Exception.Message
            throw
        }
    }
    else {
        Write-Bootstrap "shadow build reuse | buildRoot=$buildRoot"
    }

    if ($null -ne $manifest -and -not [string]::IsNullOrWhiteSpace($manifest.buildRoot) -and ($manifest.buildRoot -ne $buildRoot)) {
        Set-Content -LiteralPath $previousManifestPath -Value ($manifest | ConvertTo-Json)
    }

    Write-ShadowManifest -ManifestPath $manifestPath -Signature $sourceSignature -BuildRoot $buildRoot -ShadowDllPath $shadowDllPath
}
else {
    $shadowDllPath = $manifest.shadowDllPath
    Write-Bootstrap "shadow check | manifest current | dll=$shadowDllPath"
}

Invoke-ShadowCleanup -BuildsRoot $buildsRoot -RetiredBuildsRoot $retiredBuildsRoot -CurrentManifestPath $manifestPath -PreviousManifestPath $previousManifestPath -FailedManifestPath $failedManifestPath -RetainedBuildCount $retainedBuildCount

if ($PrepareOnly.IsPresent) {
    Write-Bootstrap "shadow prepare completed | dll=$shadowDllPath"
    Write-Output $shadowDllPath
    exit 0
}

Write-Bootstrap "launch shadow host | dll=$shadowDllPath"

& dotnet $shadowDllPath --settings $SettingsPath
$exitCode = $LASTEXITCODE

Write-Bootstrap "shadow host exit | code=$exitCode"
exit $exitCode
