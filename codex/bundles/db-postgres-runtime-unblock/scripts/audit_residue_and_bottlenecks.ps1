param(
    [string]$Root = "."
)

$ErrorActionPreference = "Stop"

# Retired provider residue should be absent from runtime paths except explicit quarantine allowlist.
$runtimePaths = @("src", "tests", "CanDoItAll.slnx")
$retiredPatterns = @(
    "UseSqlite",
    "Microsoft\.EntityFrameworkCore\.Sqlite",
    "Microsoft\.Data\.Sqlite",
    "CanDoItAll\.Migrations\.Sqlite",
    "DatabaseProviderKind\.Sqlite",
    "ManagedSqlite",
    "ExternalSqliteFile",
    "ImportedSqlite",
    "SnapshotCache",
    "IpfsSnapshot",
    "SqliteDatabaseProfileConnection",
    "SqliteDatabasePath",
    "Database\.IsSqlite\("
)

Write-Host "Scanning retired provider residue..."
foreach ($pattern in $retiredPatterns) {
    $matches = rg -n --hidden --glob "!bin/**" --glob "!obj/**" --glob "!codex/bundles/**" --glob "!.codex/**" $pattern $runtimePaths 2>$null
    if ($matches) {
        Write-Host $matches
        throw "Retired provider residue found for pattern: $pattern"
    }
}

Write-Host "Scanning hidden string concatenation residue..."
$hiddenMatches = rg -n --hidden --glob "!bin/**" --glob "!obj/**" '"Sql"\s*\+\s*"ite"|Sql"\s*\+\s*"ite' src tests 2>$null
if ($hiddenMatches) {
    Write-Host $hiddenMatches
    throw "Hidden retired-provider string concatenation found. Use explicit allowlist instead."
}

Write-Host "Scanning known DB hot-path bottleneck candidates..."
rg -n "AcquireContextLeaseAsync|BeginSwitchAsync|WaitForDrainAsync|StepDispatchGuards|Database\.IsSqlite|FOR UPDATE SKIP LOCKED|SKIP LOCKED|UPDATE .* RETURNING|AddPooledDbContextFactory" src tests

Write-Host "Audit completed."
