[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$GateId,
    [Parameter(Mandatory)][string]$GateRecordPath,
    [switch]$Execute
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$artifactRoot = Join-Path $repositoryRoot '.artifacts/agent-startup-performance/sb01-tests'
$runnerPath = Join-Path $PSScriptRoot 'Run-FrozenTests.ps1'
$suites = @('Unit','Components','Integration')
if (!$Execute) {
    [pscustomobject]@{ GateId = $GateId; Action = 'Preparation only'; Suites = $suites; Build = $false; Executes = $false }
    return
}
$gateRecord = Get-Content -LiteralPath $GateRecordPath -Raw | ConvertFrom-Json
if ($gateRecord.GateId -ne $GateId -or $gateRecord.SourceFrozen -ne $true -or $gateRecord.FocusedGatePassed -ne $true -or $gateRecord.ArtifactTreeHandedOver -ne $true -or $gateRecord.RootGoRecorded -ne $true) {
    throw 'Explicit root GO, passed focused gate, frozen source and artifact-tree handoff must be recorded before broad execution.'
}
if (@($gateRecord.Binaries).Count -lt 3) {
    throw 'The frozen record must include hashes for at least the three test assemblies.'
}
foreach ($binary in $gateRecord.Binaries) {
    $binaryPath = [IO.Path]::GetFullPath((Join-Path $artifactRoot $binary.RelativePath))
    if (!$binaryPath.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A frozen binary path escaped the owned artifact tree.'
    }
    if ((Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash -ne $binary.Sha256) {
        throw 'Frozen test binary changed after the recorded handoff.'
    }
}
if (!$PSCmdlet.ShouldProcess($GateId, 'Discover all three suites, then execute each unfiltered once with isolated test environment')) {
    return
}
$backupRoot = Join-Path $PSScriptRoot 'legacy-scenario-before'
if (Test-Path -LiteralPath $backupRoot) {
    throw 'The preservation directory already exists; inspect its ownership before starting another broad gate.'
}
$legacyRelativePath = 'artifacts/codex-bundles/project-structure-workflow-runs/proof/scenarios'
$legacyRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $legacyRelativePath))
if (!$legacyRoot.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Legacy proof root escaped the intended repository.'
}
$legacyItems = @(Get-ChildItem -LiteralPath $legacyRoot -Force -Recurse)
$rootItem = Get-Item -LiteralPath $legacyRoot -Force
if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or @($legacyItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) {
    throw 'Legacy proof subtree contains a reparse point; refusing to copy it.'
}
$legacyFiles = @($legacyItems | Where-Object { !$_.PSIsContainer })
New-Item -ItemType Directory -Path (Join-Path $backupRoot 'files') -Force | Out-Null
$preserved = foreach ($file in $legacyFiles) {
    $relativePath = [IO.Path]::GetRelativePath($legacyRoot, $file.FullName)
    $destination = [IO.Path]::GetFullPath((Join-Path (Join-Path $backupRoot 'files') $relativePath))
    if (!$destination.StartsWith($backupRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Preservation target escaped the owned directory.'
    }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    $beforeHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $beforeHash -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $beforeHash) {
        throw 'Prior proof changed during preservation; no tests were started.'
    }
    [ordered]@{ RelativePath = $relativePath; Bytes = $file.Length; Sha256 = $beforeHash }
}
[ordered]@{
    GateId = $GateId
    PreservedAtUtc = [DateTimeOffset]::UtcNow
    SourceRelativePath = $legacyRelativePath
    FileCount = $preserved.Count
    TotalBytes = ($legacyFiles | Measure-Object Length -Sum).Sum
    Files = @($preserved)
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backupRoot 'manifest.json') -Encoding utf8NoBOM
$observations = [Collections.Generic.List[object]]::new()
$discoveryFailed = $false
foreach ($suite in $suites) {
    $phase = $GateId + '-' + $suite.ToLowerInvariant()
    try {
        & $runnerPath -Suite $suite -Phase $phase -AllTests -NoBuild -Discover
        $summary = Get-Content -LiteralPath (Join-Path $PSScriptRoot "$phase-discovery.log.summary.json") -Raw | ConvertFrom-Json
        $observations.Add([ordered]@{ Suite = $suite; Phase = $phase; Discovered = [int]$summary.Discovered; DiscoverySucceeded = $true })
    } catch {
        $discoveryFailed = $true
        $observations.Add([ordered]@{ Suite = $suite; Phase = $phase; DiscoverySucceeded = $false; Error = 'Discovery failed; retained runner transcript contains details.' })
    }
}
if (!$discoveryFailed) {
    foreach ($observation in $observations) {
        if ($observation.Suite -eq 'Integration') {
            $quietPath = Join-Path $PSScriptRoot 'integration-quiet-go.json'
            $deadline = [DateTimeOffset]::UtcNow.AddMinutes(30)
            Write-Output 'Integration is waiting for the explicit application-builds-complete quiet gate.'
            [ordered]@{ GateId = $GateId; WaitingAtUtc = [DateTimeOffset]::UtcNow } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'integration-awaiting-quiet.json') -Encoding utf8NoBOM
            while (!(Test-Path -LiteralPath $quietPath)) {
                if ([DateTimeOffset]::UtcNow -gt $deadline) {
                    throw 'Integration quiet gate was not provided within30minutes; no Integration tests started.'
                }
                Start-Sleep -Seconds 1
            }
            $quiet = Get-Content -LiteralPath $quietPath -Raw | ConvertFrom-Json
            if ($quiet.GateId -ne $GateId -or $quiet.AppBuildsCompleted -ne $true -or $quiet.NoCompetingBuilds -ne $true -or $quiet.RootQuietGoRecorded -ne $true) {
                throw 'Integration quiet gate does not confirm the required root-reviewed build boundary.'
            }
        }
        try {
            & $runnerPath -Suite $observation.Suite -Phase $observation.Phase -AllTests -NoBuild -ExpectedCount $observation.Discovered
            $observation.ExecutionSucceeded = $true
        } catch {
            $observation.ExecutionSucceeded = $false
            $observation.ExecutionError = 'Execution failed or counts differed; retained TRX/summary contains details.'
        }
    }
}
$summaryPath = Join-Path $PSScriptRoot "$GateId-gate-summary.json"
[ordered]@{
    GateId = $GateId
    CompletedAtUtc = [DateTimeOffset]::UtcNow
    DiscoveryFailed = $discoveryFailed
    Suites = @($observations.ToArray())
    PreservedPriorProof = 'legacy-scenario-before/manifest.json'
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8NoBOM
Write-Output "Frozen broad gate evidence: $summaryPath"
if ($discoveryFailed -or @($observations | Where-Object { $_.ExecutionSucceeded -ne $true }).Count -gt 0) {
    throw 'Frozen broad gate did not pass; no application host was changed.'
}