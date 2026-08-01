param(
    [string]$DatabaseName = "candoitall_development",
    [string]$AppUsername = "candoitall",
    [string]$AppPassword = "candoitall",
    [string]$AdminHost = "127.0.0.1",
    [int]$AdminPort = 5432,
    [string]$AdminUsername = "postgres",
    [string]$AdminPassword = "postgres",
    [string]$AdminDatabase = "postgres",
    [string]$PsqlPath = ""
)

$ErrorActionPreference = "Stop"

function Resolve-PsqlPath {
    param([string]$ConfiguredPath)

    if (![string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        if (!(Test-Path -LiteralPath $ConfiguredPath)) {
            throw "Configured psql path was not found: $ConfiguredPath"
        }

        return (Resolve-Path -LiteralPath $ConfiguredPath).Path
    }

    $fromPath = Get-Command psql -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    $commonRoots = @(
        "C:\Program Files\PostgreSQL",
        "C:\Program Files (x86)\PostgreSQL"
    )

    foreach ($root in $commonRoots) {
        if (!(Test-Path -LiteralPath $root)) {
            continue
        }

        $candidates = Get-ChildItem -LiteralPath $root -Recurse -Filter psql.exe -ErrorAction SilentlyContinue
        $candidate = $candidates |
            Where-Object { $_.FullName -match '\\bin\\psql\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }

        $candidate = $candidates |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "psql was not found. Add PostgreSQL bin to PATH or pass -PsqlPath."
}

function Quote-PostgreSqlIdentifier {
    param([Parameter(Mandatory = $true)][string]$Value)
    return '"' + $Value.Replace('"', '""') + '"'
}

function Quote-PostgreSqlLiteral {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-PostgreSql {
    param(
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $previousPassword = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = $AdminPassword
        $output = & $script:PsqlExe `
            -h $AdminHost `
            -p $AdminPort `
            -U $AdminUsername `
            -d $Database `
            -v ON_ERROR_STOP=1 `
            @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed with exit code ${LASTEXITCODE}: $($output -join [Environment]::NewLine)"
        }

        return $output
    }
    finally {
        $env:PGPASSWORD = $previousPassword
    }
}

function Invoke-PostgreSqlScalar {
    param(
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    $result = Invoke-PostgreSql -Database $Database -Arguments @("-tA", "-c", $Sql)
    $first = $result | Select-Object -First 1
    if ($null -eq $first) {
        return ""
    }

    return $first.ToString().Trim()
}

if ($DatabaseName -notmatch '^candoitall_[a-z0-9_]+$') {
    throw "Refusing to create development database '$DatabaseName'. The name must start with 'candoitall_' and use lowercase letters, digits, and underscores."
}

$script:PsqlExe = Resolve-PsqlPath -ConfiguredPath $PsqlPath
$roleNameLiteral = Quote-PostgreSqlLiteral $AppUsername
$roleNameIdentifier = Quote-PostgreSqlIdentifier $AppUsername
$rolePasswordLiteral = Quote-PostgreSqlLiteral $AppPassword
$databaseNameLiteral = Quote-PostgreSqlLiteral $DatabaseName
$databaseNameIdentifier = Quote-PostgreSqlIdentifier $DatabaseName

Write-Host "Using psql at $script:PsqlExe"
Write-Host "Ensuring PostgreSQL role '$AppUsername' and database '$DatabaseName' on ${AdminHost}:${AdminPort}."

$roleExists = Invoke-PostgreSqlScalar -Database $AdminDatabase -Sql "select 1 from pg_roles where rolname = $roleNameLiteral;"
if ($roleExists -eq "1") {
    Invoke-PostgreSql -Database $AdminDatabase -Arguments @("-c", "alter role $roleNameIdentifier with login createdb password $rolePasswordLiteral;") | Out-Null
}
else {
    Invoke-PostgreSql -Database $AdminDatabase -Arguments @("-c", "create role $roleNameIdentifier with login createdb password $rolePasswordLiteral;") | Out-Null
}

$databaseExists = Invoke-PostgreSqlScalar -Database $AdminDatabase -Sql "select 1 from pg_database where datname = $databaseNameLiteral;"
if ($databaseExists -eq "1") {
    Invoke-PostgreSql -Database $AdminDatabase -Arguments @("-c", "alter database $databaseNameIdentifier owner to $roleNameIdentifier;") | Out-Null
}
else {
    Invoke-PostgreSql -Database $AdminDatabase -Arguments @("-c", "create database $databaseNameIdentifier owner $roleNameIdentifier;") | Out-Null
}

Invoke-PostgreSql -Database $DatabaseName -Arguments @("-c", "alter schema public owner to $roleNameIdentifier; grant all on schema public to $roleNameIdentifier;") | Out-Null

Write-Host "Development PostgreSQL database is ready."
Write-Host "Connection string: Host=127.0.0.1;Port=5432;Database=$DatabaseName;Username=$AppUsername;Password=$AppPassword;Include Error Detail=true"
