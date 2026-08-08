[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot "CanDoItAll.slnx") -PathType Leaf)) {
    throw "Repository root does not contain CanDoItAll.slnx."
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$statusRows = [System.Collections.Generic.List[object]]::new()
$failures = 0

function Invoke-BaselineStep {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $logPath = Join-Path $OutputRoot "$Name.log"
    $exitCode = 1
    Write-Host "=== $Name ==="
    Push-Location $RepoRoot
    try {
        & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $logPath
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($null -eq $exitCode) {
        $exitCode = 0
    }
    $statusRows.Add([pscustomobject]@{
        Step = $Name
        ExitCode = [int]$exitCode
        Log = [System.IO.Path]::GetFileName($logPath)
    })
    return [int]$exitCode
}

$gitHead = (& git -C $RepoRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0) {
    $gitHead = "unknown"
}
$gitBranch = (& git -C $RepoRoot branch --show-current 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitBranch)) {
    $gitBranch = "detached"
}
$gitStatus = (& git -C $RepoRoot status --short 2>$null)
$hostMetadata = [ordered]@{
    schema_version = 1
    host_os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    host_architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    process_architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    repository_head = "$gitHead".Trim()
    repository_branch = "$gitBranch".Trim()
    repository_dirty = -not [string]::IsNullOrWhiteSpace(($gitStatus -join [Environment]::NewLine))
    configuration = $Configuration
}
$hostMetadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRoot "host-metadata.json") -Encoding utf8

$steps = @(
    @{ Name = "dotnet-info"; File = "dotnet"; Arguments = @("--info") },
    @{ Name = "git-status"; File = "git"; Arguments = @("status", "--short", "--branch") },
    @{ Name = "restore"; File = "dotnet"; Arguments = @("restore", "./CanDoItAll.slnx") },
    @{ Name = "build"; File = "dotnet"; Arguments = @("build", "./CanDoItAll.slnx", "-c", $Configuration, "--no-restore", "/m:1") },
    @{ Name = "stable-tests"; File = "dotnet"; Arguments = @("test", "./CanDoItAll.slnx", "-c", $Configuration, "--no-build", "--filter", "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined", "/m:1") }
)

foreach ($step in $steps) {
    $exitCode = Invoke-BaselineStep -Name $step.Name -FilePath $step.File -Arguments $step.Arguments
    if ($exitCode -ne 0) {
        $failures++
    }
}

$secretScanner = Join-Path $PSScriptRoot "scan_artifacts_for_secrets.py"
if (Test-Path -LiteralPath $secretScanner -PathType Leaf) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) {
        $pythonCommand = Get-Command py -ErrorAction SilentlyContinue
    }
    if ($null -eq $pythonCommand) {
        throw "Python is required to run the artifact secret scanner."
    }
    $pythonArguments = if ($pythonCommand.Name -eq "py.exe" -or $pythonCommand.Name -eq "py") {
        @("-3", $secretScanner, "--root", $OutputRoot, "--output", (Join-Path $OutputRoot "secret-scan.json"))
    }
    else {
        @($secretScanner, "--root", $OutputRoot, "--output", (Join-Path $OutputRoot "secret-scan.json"))
    }
    & $pythonCommand.Source @pythonArguments
    $secretExit = $LASTEXITCODE
    $statusRows.Add([pscustomobject]@{
        Step = "secret-scan"
        ExitCode = [int]$secretExit
        Log = "secret-scan.json"
    })
    if ($secretExit -ne 0) {
        $failures++
    }
}

$statusRows | Export-Csv -LiteralPath (Join-Path $OutputRoot "step-status.csv") -NoTypeInformation -Encoding utf8
$summary = [System.Collections.Generic.List[string]]::new()
$summary.Add("# Baseline summary")
$summary.Add("")
$summary.Add("- Repository: ``$RepoRoot``")
$summary.Add("- Commit: ``$($hostMetadata.repository_head)``")
$summary.Add("- Branch: ``$($hostMetadata.repository_branch)``")
$summary.Add("- Dirty checkout: ``$($hostMetadata.repository_dirty.ToString().ToLowerInvariant())``")
$summary.Add("- Host: ``$($hostMetadata.host_os)``")
$summary.Add("- Configuration: ``$Configuration``")
$summary.Add("- Failed steps: $failures")
$summary.Add("")
$summary.Add("## Step status")
$summary.Add("")
$summary.Add("| Step | Exit code | Log |")
$summary.Add("|---|---:|---|")
foreach ($row in $statusRows) {
    $summary.Add("| $($row.Step) | $($row.ExitCode) | ``$($row.Log)`` |")
}
$summary | Set-Content -LiteralPath (Join-Path $OutputRoot "baseline-summary.md") -Encoding utf8

Write-Host "Baseline evidence: $OutputRoot"
if ($failures -ne 0) {
    Write-Host "RESULT: FAIL ($failures failed step(s))"
    exit 1
}

Write-Host "RESULT: PASS"
