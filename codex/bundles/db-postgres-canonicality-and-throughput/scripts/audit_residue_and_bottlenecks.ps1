param(
    [string]$Root = "."
)

$ErrorActionPreference = "Stop"

function Invoke-RipgrepAudit {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & rg @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -gt 1)
    {
        throw "rg audit failed with exit code $exitCode for arguments: $($Arguments -join ' ')"
    }

    if ($exitCode -eq 1)
    {
        Write-Host "(no matches)"
    }
}

$srcPath = Join-Path $Root "src"
$testsPath = Join-Path $Root "tests"
$solutionPath = Join-Path $Root "CanDoItAll.slnx"
$modulePaths = Get-ChildItem -Path $srcPath -Directory -Filter "CanDoItAll.Modules.*" |
    ForEach-Object { $_.FullName }

Write-Host "== Retired provider residue audit =="
Invoke-RipgrepAudit -Arguments @(
    "-n",
    "-i",
    "usesqlite|migrations\.sqlite|SqliteWriteCoordination|DatabaseSnapshots|IDatabaseSnapshotService",
    $srcPath,
    $testsPath,
    $solutionPath)

Write-Host "== Allowed quarantine term audit =="
Invoke-RipgrepAudit -Arguments @(
    "-n",
    "RetiredProviderName|Sqlite",
    (Join-Path $srcPath "CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs"))

Write-Host "== Hot switching/drain audit =="
Invoke-RipgrepAudit -Arguments @(
    "-n",
    "AcquireContextLeaseAsync|BeginSwitchAsync|WaitForDrainAsync|DatabaseContextLease|DatabaseSwitchSession|EnableMaintenanceHotSwitch",
    $srcPath,
    $testsPath)

Write-Host "== Profile-specific context audit =="
Invoke-RipgrepAudit -Arguments @(
    "-n",
    "CreateDbContextForProfileAsync|ISwitchableAppDbContextFactory",
    $srcPath,
    $testsPath)

Write-Host "== PostgreSQL claim audit =="
$postgresClaimAuditArguments = @(
    "-n",
    "FOR UPDATE SKIP LOCKED|LeaseToken|AutomationDispatchClaimToken|AutomationDispatchLeaseExpiresAtUtc") + $modulePaths
Invoke-RipgrepAudit -Arguments $postgresClaimAuditArguments
