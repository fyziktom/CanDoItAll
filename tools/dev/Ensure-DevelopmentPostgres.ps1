[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$DatabaseName = "candoitall_development",
    [string]$AppUsername = "candoitall",
    [string]$AppPassword = "candoitall",
    [string]$AdminHost = "127.0.0.1",
    [int]$AdminPort = 5432,
    [string]$AdminUsername = "postgres",
    [string]$AdminPassword = "postgres",
    [string]$AdminDatabase = "postgres",
    [Parameter(Mandatory = $true)]
    [string]$PsqlPath
)

$ErrorActionPreference = "Stop"

function Resolve-PsqlPath {
    param([Parameter(Mandatory = $true)][string]$ConfiguredPath)

    if (-not (Test-Path -LiteralPath $ConfiguredPath -PathType Leaf)) {
        throw "Configured psql path was not found: $ConfiguredPath"
    }
    $resolvedPath = (Resolve-Path -LiteralPath $ConfiguredPath).Path
    $version = @(& $resolvedPath --version 2>&1)
    if ($LASTEXITCODE -ne 0 -or ($version -join " ") -notmatch 'PostgreSQL\)\s+18\.') {
        throw "Select a verified PostgreSQL 18 psql executable with -PsqlPath."
    }
    return $resolvedPath
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
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$InputSql = ""
    )

    $previousPassword = $env:PGPASSWORD
    $previousClientEncoding = $env:PGCLIENTENCODING
    $previousOutputEncoding = $OutputEncoding
    $previousErrorPreference = $ErrorActionPreference
    try {
        $env:PGPASSWORD = $AdminPassword
        $env:PGCLIENTENCODING = "UTF8"
        $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $ErrorActionPreference = "Continue"
        $output = $InputSql | & $script:PsqlExe `
            -X -w `
            -h $AdminHost `
            -p $AdminPort `
            -U $AdminUsername `
            -d $Database `
            -v ON_ERROR_STOP=1 `
            @Arguments 2>&1
        $ErrorActionPreference = $previousErrorPreference
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed on ${AdminHost}:${AdminPort}/$Database with exit code ${LASTEXITCODE}. SQL and server detail are suppressed to protect credentials."
        }

        return $output
    }
    finally {
        $env:PGPASSWORD = $previousPassword
        $env:PGCLIENTENCODING = $previousClientEncoding
        $OutputEncoding = $previousOutputEncoding
        $ErrorActionPreference = $previousErrorPreference
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

$serverVersion = Invoke-PostgreSqlScalar -Database $AdminDatabase -Sql "show server_version_num;"
if ($serverVersion -notmatch '^18[0-9]{4}$') {
    throw "Expected a PostgreSQL 18 target; observed server_version_num=$serverVersion. Existing clusters must follow tools/dev/Migrate-PostgreSql16To18.md."
}
Write-Host "Verified target server_version_num=$serverVersion."

$roleExists = Invoke-PostgreSqlScalar -Database $AdminDatabase -Sql "select 1 from pg_roles where rolname = $roleNameLiteral;"
if ($roleExists -eq "1") {
    $roleReady = Invoke-PostgreSqlScalar -Database $AdminDatabase -Sql "select rolcanlogin and rolcreatedb from pg_roles where rolname = $roleNameLiteral;"
    if ($roleReady -ne "t") {
        throw "Existing role '$AppUsername' needs LOGIN and CREATEDB. Review its grants explicitly; this helper does not modify existing roles or passwords."
    }
    Write-Host "Retaining the existing role and its credentials."
}
elseif ($PSCmdlet.ShouldProcess("${AdminHost}:${AdminPort}/$AppUsername", "Create development login role")) {
    Invoke-PostgreSql -Database $AdminDatabase -Arguments @("-f", "-") -InputSql "create role $roleNameIdentifier with login createdb password $rolePasswordLiteral;" | Out-Null
}

$databaseOwner = Invoke-PostgreSqlScalar -Database $AdminDatabase -Sql "select pg_get_userbyid(datdba) from pg_database where datname = $databaseNameLiteral;"
if (-not [string]::IsNullOrWhiteSpace($databaseOwner)) {
    if ($databaseOwner -ne $AppUsername) {
        throw "Existing database '$DatabaseName' belongs to '$databaseOwner'. Refusing an implicit ownership change."
    }
}
elseif ($PSCmdlet.ShouldProcess("${AdminHost}:${AdminPort}/$DatabaseName", "Create development database")) {
    Invoke-PostgreSql -Database $AdminDatabase -Arguments @("-c", "create database $databaseNameIdentifier owner $roleNameIdentifier;") | Out-Null
}

Write-Host "Development PostgreSQL target: ${AdminHost}:${AdminPort}/$DatabaseName; role=$AppUsername. Credentials are not displayed or reset."
