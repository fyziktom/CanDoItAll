[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$ProjectPath = "",
    [string]$SettingsPath = "",
    [string]$ShadowArtifactsPath = "",
    [string]$Configuration = "Debug",
    [switch]$ForceRebuild
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
$manifestPath = Join-Path $ShadowArtifactsPath "current.json"
$configurationSegment = $Configuration.ToLowerInvariant()

New-Item -ItemType Directory -Force -Path $buildsRoot | Out-Null

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
    elseif (-not (Test-Path -LiteralPath $manifest.shadowDllPath)) {
        $shadowNeedsRefresh = $true
        Write-Bootstrap "shadow check | manifest dll missing | path=$($manifest.shadowDllPath)"
    }
}

if ($shadowNeedsRefresh) {
    $buildRootName = $sourceSignature
    if ($ForceRebuild.IsPresent) {
        $buildRootName = "{0}-{1}" -f $sourceSignature, ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
    }

    $buildRoot = Join-Path $buildsRoot $buildRootName
    $shadowDllPath = Join-Path $buildRoot "bin\CanDoItAll.Mcp.DotNetWatch\$configurationSegment\CanDoItAll.Mcp.DotNetWatch.dll"

    if (-not (Test-Path -LiteralPath $shadowDllPath)) {
        Write-Bootstrap "shadow build start | buildRoot=$buildRoot"

        $buildOutput = & dotnet build $ProjectPath -c $Configuration --artifacts-path $buildRoot -p:UseAppHost=false 2>&1
        foreach ($line in $buildOutput) {
            $text = $line.ToString()
            [Console]::Error.WriteLine($text)
            Add-Content -Path $script:BootstrapLogPath -Value ("{0} build | {1}" -f [DateTimeOffset]::UtcNow.ToString("O"), $text)
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Shadow build failed with exit code $LASTEXITCODE. See $script:BootstrapLogPath."
        }

        if (-not (Test-Path -LiteralPath $shadowDllPath)) {
            throw "Shadow build completed without producing '$shadowDllPath'."
        }

        Write-Bootstrap "shadow build completed | buildRoot=$buildRoot"
    }
    else {
        Write-Bootstrap "shadow build reuse | buildRoot=$buildRoot"
    }

    Write-ShadowManifest -ManifestPath $manifestPath -Signature $sourceSignature -BuildRoot $buildRoot -ShadowDllPath $shadowDllPath
}
else {
    $shadowDllPath = $manifest.shadowDllPath
    Write-Bootstrap "shadow check | manifest current | dll=$shadowDllPath"
}

Write-Bootstrap "launch shadow host | dll=$shadowDllPath"

& dotnet $shadowDllPath --settings $SettingsPath
$exitCode = $LASTEXITCODE

Write-Bootstrap "shadow host exit | code=$exitCode"
exit $exitCode
