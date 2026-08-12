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
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]]$Findings,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $Findings.Add($Message)
}

$repositoryRoot = Resolve-RepositoryRoot -RequestedPath $RepositoryPath
$composePath = Join-Path $repositoryRoot "compose.yaml"
$environmentPath = Join-Path $repositoryRoot ".env.example"
$dockerIgnorePath = Join-Path $repositoryRoot ".dockerignore"
$dockerfilePath = Join-Path $repositoryRoot "src\App\CanDoItAll.Web\Dockerfile"
$findings = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    Add-Finding -Findings $findings -Message "Missing canonical compose.yaml."
}

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    Add-Finding -Findings $findings -Message "Missing .env.example."
}

if (-not (Test-Path -LiteralPath $dockerIgnorePath -PathType Leaf)) {
    Add-Finding -Findings $findings -Message "Missing root .dockerignore for the application build context."
}

if (-not (Test-Path -LiteralPath $dockerfilePath -PathType Leaf)) {
    Add-Finding -Findings $findings -Message "Missing CanDoItAll.Web Dockerfile."
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

    if ($composeContent -notmatch "(?ms)^\s{2}app:\s.*?condition:\s*service_healthy") {
        Add-Finding -Findings $findings -Message "The application must wait for a healthy database."
    }

    if ($composeContent -notmatch "(?m)^\s{6}Database__PasswordFile:\s*/run/secrets/db-password\s*$") {
        Add-Finding -Findings $findings -Message "The application must consume the database password through the Compose secret file."
    }

    if ($composeContent -notmatch "(?m)^\s{2}app-data:\s*$" -or
        $composeContent -notmatch "(?m)^\s{2}db-data:\s*$") {
        Add-Finding -Findings $findings -Message "Application and database persistence must use explicit named volumes."
    }

    if ($composeContent -notmatch "(?m)^\s*restart\s*:\s*['""]?no['""]?\s*$") {
        Add-Finding -Findings $findings -Message "Development services must expose failures with restart set to no."
    }
}

if (Test-Path -LiteralPath $dockerfilePath -PathType Leaf) {
    $dockerfileContent = Get-Content -LiteralPath $dockerfilePath -Raw
    if ($dockerfileContent -notmatch "(?m)^FROM\s+.+\s+AS\s+build\s*$" -or
        $dockerfileContent -notmatch "(?m)^FROM\s+mcr\.microsoft\.com/dotnet/aspnet:\$\{DOTNET_RUNTIME_VERSION\}\s+AS\s+runtime\s*$") {
        Add-Finding -Findings $findings -Message "The application Dockerfile must use separate build and ASP.NET runtime stages."
    }

    if ($dockerfileContent -notmatch '(?m)^USER\s+\$APP_UID\s*$') {
        Add-Finding -Findings $findings -Message "The application Dockerfile must select the non-root .NET app user."
    }

    if ($dockerfileContent -notmatch '(?m)^ENTRYPOINT\s+\["dotnet",\s*"CanDoItAll\.Web\.dll"\]\s*$') {
        Add-Finding -Findings $findings -Message "The application Dockerfile must use the JSON-form web entry point."
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
