[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$MainRepoRoot,

    [string]$OriginalBranch = "ui-refactoring",

    [string]$ForbiddenBranch = "ui-refactoring-v2",

    [string]$Remote = "origin",

    [string]$Head = "HEAD",

    [string]$OutputDirectory = ".artifacts/ui-refactoring-integration/scope"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo = [System.IO.Path]::GetFullPath($MainRepoRoot)
if (-not (Test-Path -LiteralPath (Join-Path $repo ".git"))) {
    throw "MainRepoRoot is not a Git repository: $repo"
}

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = & git -C $repo @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$output"
    }

    return @($output)
}

Invoke-Git @("fetch", $Remote, "--prune") | Out-Null

$originalRef = "$Remote/$OriginalBranch"
$forbiddenRef = "$Remote/$ForbiddenBranch"

Invoke-Git @("rev-parse", "--verify", $originalRef) | Out-Null
Invoke-Git @("rev-parse", "--verify", $forbiddenRef) | Out-Null
Invoke-Git @("rev-parse", "--verify", $Head) | Out-Null

$forbiddenCommits = @(
    Invoke-Git @("rev-list", "$originalRef..$forbiddenRef") |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
)

if ($forbiddenCommits.Count -eq 0) {
    throw "The forbidden branch has no unique commits relative to the original branch. Refresh scope analysis before continuing."
}

$violations = [System.Collections.Generic.List[string]]::new()
foreach ($commit in $forbiddenCommits) {
    & git -C $repo merge-base --is-ancestor $commit $Head 2>$null
    if ($LASTEXITCODE -eq 0) {
        $violations.Add($commit)
    }
    elseif ($LASTEXITCODE -ne 1) {
        throw "Could not evaluate ancestry for forbidden commit $commit."
    }
}

& git -C $repo merge-base --is-ancestor $forbiddenRef $Head 2>$null
$forbiddenHeadIsAncestor = $LASTEXITCODE -eq 0
if ($LASTEXITCODE -notin @(0, 1)) {
    throw "Could not evaluate forbidden branch ancestry."
}

$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
}
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$record = [ordered]@{
    checkedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    repository = $repo
    head = @(Invoke-Git @("rev-parse", $Head))[0]
    originalRef = $originalRef
    originalHead = @(Invoke-Git @("rev-parse", $originalRef))[0]
    forbiddenRef = $forbiddenRef
    forbiddenHead = @(Invoke-Git @("rev-parse", $forbiddenRef))[0]
    forbiddenUniqueCommitCount = $forbiddenCommits.Count
    forbiddenUniqueCommits = $forbiddenCommits
    forbiddenHeadIsAncestor = $forbiddenHeadIsAncestor
    violatingCommits = @($violations)
}
$record | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $outputRoot "scope-verification.json") -Encoding utf8

if ($forbiddenHeadIsAncestor -or $violations.Count -gt 0) {
    throw "Forbidden ui-refactoring-v2 history is present in '$Head'. See scope-verification.json."
}

Write-Host "Scope guard passed. Checked $($forbiddenCommits.Count) commits unique to $forbiddenRef."
