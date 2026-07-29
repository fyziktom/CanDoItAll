[CmdletBinding()]
param(
    [string]$RepositoryPath = "",
    [switch]$SkipDockerCommand
)

$ErrorActionPreference = "Stop"

function Resolve-RepositoryRoot {
    param(
        [string]$RequestedPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
}

function Add-Finding {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.Generic.List[string]]$Findings,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $Findings.Add($Message)
}

$repositoryRoot = Resolve-RepositoryRoot -RequestedPath $RepositoryPath
$composePath = Join-Path $repositoryRoot "compose.yaml"
$environmentPath = Join-Path $repositoryRoot ".env.example"
$findings = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    Add-Finding -Findings $findings -Message "Missing canonical compose.yaml."
}

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    Add-Finding -Findings $findings -Message "Missing .env.example."
}

if ($findings.Count -eq 0) {
    $composeContent = Get-Content -LiteralPath $composePath -Raw

    if ($composeContent -match "(?m)^\s*version\s*:") {
        Add-Finding -Findings $findings -Message "compose.yaml contains the obsolete top-level version property."
    }

    if ($composeContent -match "(?m)^\s*container_name\s*:") {
        Add-Finding -Findings $findings -Message "compose.yaml sets container_name and prevents Compose isolation."
    }

    if ($composeContent -match "(?m)^\s*image\s*:\s*[^\r\n]*(?::latest|\$\{[^}]*:-latest\})\s*$") {
        Add-Finding -Findings $findings -Message "compose.yaml uses a latest image tag."
    }

    if ($composeContent -notmatch "\$\{CDA_BIND_ADDRESS:-127\.0\.0\.1\}") {
        Add-Finding -Findings $findings -Message "Published development ports must default to loopback."
    }

    if ($composeContent -notmatch "(?m)^\s*restart\s*:\s*['""]?no['""]?\s*$") {
        Add-Finding -Findings $findings -Message "Development services must expose failures with restart set to no."
    }
}

if (-not $SkipDockerCommand -and $findings.Count -eq 0) {
    if ($null -eq (Get-Command docker -ErrorAction SilentlyContinue)) {
        Add-Finding -Findings $findings -Message "Docker is unavailable. Install Compose v2 or rerun static validation with -SkipDockerCommand."
    }
    else {
        & docker compose --env-file $environmentPath --file $composePath config --quiet
        if ($LASTEXITCODE -ne 0) {
            Add-Finding -Findings $findings -Message "docker compose config failed with exit code $LASTEXITCODE."
        }
    }
}

if ($findings.Count -gt 0) {
    $findings |
        Sort-Object -Unique |
        ForEach-Object { Write-Host "ERROR: $_" -ForegroundColor Red }
    throw "Docker validation failed with $($findings.Count) finding(s)."
}

Write-Host "Docker validation passed."
