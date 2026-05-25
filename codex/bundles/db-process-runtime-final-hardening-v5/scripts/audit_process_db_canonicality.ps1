# Audit process DB canonicality surfaces.
# This script is intentionally conservative and should be refined by Codex during execution.

$ErrorActionPreference = "Stop"

function Invoke-RipgrepAudit {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Label,
        [Parameter(Mandatory = $true)]
        [string] $Pattern,
        [Parameter(Mandatory = $true)]
        [string[]] $Paths
    )

    Write-Host "== $Label =="
    & rg -n -i $Pattern @Paths
    if ($LASTEXITCODE -gt 1) {
        throw "rg failed for audit '$Label' with exit code $LASTEXITCODE"
    }
}

Write-Host "== SQLite runtime residue =="
Invoke-RipgrepAudit `
    -Label "SQLite runtime residue" `
    -Pattern "UseSqlite|Migrations\.Sqlite|SqliteWriteCoordination|DatabaseProviderKind\.Sqlite|ManagedSqlite|ExternalSqliteFile|ImportedSqlite|SnapshotCache|IpfsSnapshot" `
    -Paths @("src", "tests", "CanDoItAll.slnx")

Invoke-RipgrepAudit `
    -Label "Process lease release patterns" `
    -Pattern "LeaseToken\s*=\s*string\.Empty|LeaseExpiresAtUtc\s*=\s*null|Release.*Lease|Finalize.*Claim|TryFinalize" `
    -Paths @("src/CanDoItAll.Modules.Processes", "src/CanDoItAll.Modules.Workspace", "src/CanDoItAll.Modules.Automation")

Invoke-RipgrepAudit `
    -Label "PostgreSQL claim patterns" `
    -Pattern "FOR UPDATE SKIP LOCKED|ExecuteUpdateAsync|LeaseExpiresAtUtc|AutomationDispatchLeaseExpiresAtUtc|LockToken|LockedAtUtc" `
    -Paths @("src/CanDoItAll.Modules.Processes", "src/CanDoItAll.Modules.Workspace", "src/CanDoItAll.Modules.Automation")
