param(
    [string]$ConnectionString = "Host=127.0.0.1;Port=5432;Database=candoitall_workflow_routing_dev;Username=candoitall;Password=candoitall;Include Error Detail=true",
    [switch]$SkipMigrations
)

$ErrorActionPreference = "Stop"

function Get-ConnectionValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,
        [Parameter(Mandatory = $true)]
        [string]$Key
    )

    foreach ($part in $ConnectionString.Split(";", [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $separatorIndex = $part.IndexOf("=")
        if ($separatorIndex -lt 1) {
            continue
        }

        $partKey = $part.Substring(0, $separatorIndex).Trim()
        if (![string]::Equals($partKey, $Key, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        return $part.Substring($separatorIndex + 1).Trim()
    }

    return ""
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

$databaseName = Get-ConnectionValue -ConnectionString $ConnectionString -Key "Database"
if ([string]::IsNullOrWhiteSpace($databaseName)) {
    throw "Connection string must include a Database value."
}

if ($databaseName -notmatch '^candoitall_workflow_routing[a-z0-9_-]*$') {
    throw "Refusing to reset PostgreSQL database '$databaseName'. The name must start with 'candoitall_workflow_routing'."
}

$forbiddenDatabases = @("postgres", "template0", "template1")
if ($forbiddenDatabases -contains $databaseName.ToLowerInvariant()) {
    throw "Refusing to reset PostgreSQL system database '$databaseName'."
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$migrationProject = Join-Path $repoRoot "src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj"
$startupProject = Join-Path $repoRoot "src\CanDoItAll.Web\CanDoItAll.Web.csproj"

if (!(Test-Path -LiteralPath $migrationProject)) {
    throw "PostgreSQL migration project was not found: $migrationProject"
}

if (!(Test-Path -LiteralPath $startupProject)) {
    throw "Web startup project was not found: $startupProject"
}

$env:CANDOITALL_DATABASE_PROVIDER = "PostgreSql"
$env:CANDOITALL_DATABASE_CONNECTION = $ConnectionString
$env:Database__Provider = "PostgreSql"
$env:Database__ConnectionString = $ConnectionString

Push-Location $repoRoot
try {
    Write-Host "Resetting PostgreSQL database '$databaseName' for workflow routing validation."
    Invoke-CheckedCommand -FilePath "dotnet" -Arguments @(
        "ef",
        "database",
        "drop",
        "--force",
        "--project",
        $migrationProject,
        "--startup-project",
        $startupProject)

    if (!$SkipMigrations) {
        Invoke-CheckedCommand -FilePath "dotnet" -Arguments @(
            "ef",
            "database",
            "update",
            "--project",
            $migrationProject,
            "--startup-project",
            $startupProject)
    }

    Write-Host "PostgreSQL workflow-routing database '$databaseName' is clean."
}
finally {
    Pop-Location
}
