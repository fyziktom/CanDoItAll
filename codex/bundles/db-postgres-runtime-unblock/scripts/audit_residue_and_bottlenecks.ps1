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
$allowedRetiredResidue = @{
    "ManagedSqlite" = @("src\CanDoItAll.Infrastructure\ControlPlane\LegacyDatabaseProfileCatalogQuarantine.cs")
    "ExternalSqliteFile" = @("src\CanDoItAll.Infrastructure\ControlPlane\LegacyDatabaseProfileCatalogQuarantine.cs")
    "ImportedSqlite" = @("src\CanDoItAll.Infrastructure\ControlPlane\LegacyDatabaseProfileCatalogQuarantine.cs")
    "SnapshotCache" = @("src\CanDoItAll.Infrastructure\ControlPlane\LegacyDatabaseProfileCatalogQuarantine.cs")
    "IpfsSnapshot" = @("src\CanDoItAll.Infrastructure\ControlPlane\LegacyDatabaseProfileCatalogQuarantine.cs")
}

function Test-AllowedRetiredResidue {
    param(
        [string]$Pattern,
        [string]$MatchLine
    )

    if (-not $allowedRetiredResidue.ContainsKey($Pattern)) {
        return $false
    }

    foreach ($allowedPath in $allowedRetiredResidue[$Pattern]) {
        if ($MatchLine.StartsWith($allowedPath, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

Write-Host "Scanning retired provider residue..."
foreach ($pattern in $retiredPatterns) {
    $matches = rg -n --hidden --glob "!bin/**" --glob "!obj/**" --glob "!codex/bundles/**" --glob "!.codex/**" $pattern $runtimePaths 2>$null
    $unexpectedMatches = @($matches | Where-Object { -not (Test-AllowedRetiredResidue -Pattern $pattern -MatchLine $_) })
    if ($unexpectedMatches) {
        Write-Host $unexpectedMatches
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
