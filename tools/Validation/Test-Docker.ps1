[CmdletBinding()]
param(
    [string]$RepositoryPath = "",
    [switch]$SkipDockerCommand,
    [switch]$RunNegativeFixtures
)

$ErrorActionPreference = "Stop"

function Resolve-RepositoryRoot {
    param([string]$RequestedPath)

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

function Get-ComposeServiceBlock {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ComposeContent,
        [Parameter(Mandatory = $true)]
        [string]$ServiceName
    )

    $normalizedContent = $ComposeContent.Replace("`r`n", "`n")
    $escapedName = [Regex]::Escape($ServiceName)
    $match = [Regex]::Match(
        $normalizedContent,
        "(?ms)^  ${escapedName}:\s*`n(?<block>.*?)(?=^  [a-zA-Z0-9_.-]+:\s*`n|\z)")
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups["block"].Value
}

function Test-ComposePolicy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ComposeContent,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]]$Findings
    )

    if ($ComposeContent -match "(?m)^\s*version\s*:") {
        Add-Finding -Findings $Findings -Message "compose.yaml contains the obsolete top-level version property."
    }

    if ($ComposeContent -match "(?m)^\s*container_name\s*:") {
        Add-Finding -Findings $Findings -Message "compose.yaml sets container_name and prevents Compose isolation."
    }

    if ($ComposeContent -match "(?m)^\s*image\s*:\s*[^\r\n]*(?::latest|\$\{[^}]*:-latest\})\s*$") {
        Add-Finding -Findings $Findings -Message "compose.yaml uses a latest image tag."
    }

    $appBlock = Get-ComposeServiceBlock -ComposeContent $ComposeContent -ServiceName "app"
    $dbBlock = Get-ComposeServiceBlock -ComposeContent $ComposeContent -ServiceName "db"
    if ($null -eq $appBlock) {
        Add-Finding -Findings $Findings -Message "compose.yaml must define the app service."
    }

    if ($null -eq $dbBlock) {
        Add-Finding -Findings $Findings -Message "compose.yaml must define the db service."
    }

    $serviceBlocks = @{
        app = $appBlock
        db = $dbBlock
    }
    foreach ($serviceName in @("app", "db")) {
        $serviceBlock = $serviceBlocks[$serviceName]
        if ($null -eq $serviceBlock) {
            continue
        }

        if ($serviceBlock -notmatch '(?m)^    restart\s*:\s*[''\"]?no[''\"]?\s*$') {
            Add-Finding -Findings $Findings -Message "The $serviceName service must expose failures with restart set to no."
        }

        if ($serviceBlock -notmatch "(?m)^    logging\s*:\s*\*bounded-logging\s*$") {
            Add-Finding -Findings $Findings -Message "The $serviceName service must use bounded logging."
        }

        foreach ($requiredField in @("healthcheck", "mem_limit", "cpus", "pids_limit", "stop_grace_period")) {
            if ($serviceBlock -notmatch "(?m)^    $([Regex]::Escape($requiredField))\s*:") {
                Add-Finding -Findings $Findings -Message "The $serviceName service must define $requiredField."
            }
        }

        if ($serviceBlock -notmatch "(?ms)^    secrets\s*:\s*`n(?: {6}.*`n)*?      - db-password\s*$") {
            Add-Finding -Findings $Findings -Message "The $serviceName service must consume the db-password secret."
        }
    }

    if ($null -ne $appBlock) {
        if ($appBlock -notmatch "\$\{CDA_BIND_ADDRESS:-127\.0\.0\.1\}") {
            Add-Finding -Findings $Findings -Message "The app service published port must default to loopback."
        }

        if ($appBlock -notmatch "(?ms)^    depends_on\s*:.*?^        condition\s*:\s*service_healthy\s*$") {
            Add-Finding -Findings $Findings -Message "The app service must wait for a healthy database."
        }

        if ($appBlock -notmatch "(?m)^      Database__PasswordFile:\s*/run/secrets/db-password\s*$") {
            Add-Finding -Findings $Findings -Message "The app service must consume the database password through the Compose secret file."
        }

        if ($appBlock -notmatch "(?m)^      - app-data:/data\s*$") {
            Add-Finding -Findings $Findings -Message "The app service must persist data through app-data."
        }

        foreach ($requiredSecurityContract in @(
            "(?m)^    init\s*:\s*true\s*$",
            "(?m)^    read_only\s*:\s*true\s*$",
            "(?m)^      - ALL\s*$",
            "(?m)^      - no-new-privileges:true\s*$")) {
            if ($appBlock -notmatch $requiredSecurityContract) {
                Add-Finding -Findings $Findings -Message "The app service is missing a required least-privilege contract."
            }
        }
    }

    if ($null -ne $dbBlock) {
        if ($dbBlock -notmatch "(?m)^      POSTGRES_PASSWORD_FILE:\s*/run/secrets/db-password\s*$") {
            Add-Finding -Findings $Findings -Message "The db service must consume POSTGRES_PASSWORD_FILE through the Compose secret."
        }

        if ($dbBlock -notmatch "(?m)^      - db-data:/var/lib/postgresql/data\s*$") {
            Add-Finding -Findings $Findings -Message "The db service must persist data through db-data."
        }

        if ($dbBlock -match "(?m)^    ports\s*:") {
            Add-Finding -Findings $Findings -Message "The db service must not publish a host port."
        }
    }

    if ($ComposeContent -notmatch "(?m)^  app-data:\s*$" -or
        $ComposeContent -notmatch "(?m)^  db-data:\s*$") {
        Add-Finding -Findings $Findings -Message "Application and database persistence must use explicit named volumes."
    }

    if ($ComposeContent.Replace("`r`n", "`n") -notmatch "(?ms)^  backend:\s*`n    internal:\s*true\s*$") {
        Add-Finding -Findings $Findings -Message "The database backend network must remain internal."
    }
}

function Test-ComposeNegativeFixtures {
    param([Parameter(Mandatory = $true)][string]$ComposeContent)

    $normalizedContent = $ComposeContent.Replace("`r`n", "`n")
    $fixtures = @(
        @{
            Name = "db restart"
            Content = $normalizedContent.Replace(
                "    stop_grace_period: 60s`n    restart: `"no`"`n",
                "    stop_grace_period: 60s`n")
            Expected = "The db service must expose failures with restart set to no."
        },
        @{
            Name = "db password file"
            Content = $normalizedContent.Replace("      POSTGRES_PASSWORD_FILE: /run/secrets/db-password`n", "")
            Expected = "The db service must consume POSTGRES_PASSWORD_FILE through the Compose secret."
        },
        @{
            Name = "app loopback"
            Content = $normalizedContent.Replace('${CDA_BIND_ADDRESS:-127.0.0.1}', '${CDA_BIND_ADDRESS:-0.0.0.0}')
            Expected = "The app service published port must default to loopback."
        }
    )

    foreach ($fixture in $fixtures) {
        $fixtureFindings = [System.Collections.Generic.List[string]]::new()
        Test-ComposePolicy -ComposeContent $fixture.Content -Findings $fixtureFindings
        if (-not $fixtureFindings.Contains($fixture.Expected)) {
            throw "Docker negative fixture '$($fixture.Name)' did not produce its expected finding."
        }
    }
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
    Test-ComposePolicy -ComposeContent $composeContent -Findings $findings
    if ($RunNegativeFixtures) {
        Test-ComposeNegativeFixtures -ComposeContent $composeContent
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

    if ($dockerfileContent -notmatch '(?s)apt-get\s+install\s+--yes\s+--no-install-recommends(?:(?!&&).)*\butil-linux\b') {
        Add-Finding -Findings $findings -Message "The application runtime image must explicitly install util-linux for setsid process-group bootstrap."
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
