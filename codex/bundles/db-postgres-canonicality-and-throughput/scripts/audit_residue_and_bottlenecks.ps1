param(
    [string]$Root = "."
)

$ErrorActionPreference = "Stop"

Write-Host "== Retired provider residue audit =="
rg -n -i "usesqlite|migrations\.sqlite|SqliteWriteCoordination|DatabaseSnapshots|IDatabaseSnapshotService" "$Root/src" "$Root/tests" "$Root/CanDoItAll.slnx"

Write-Host "== Allowed quarantine term audit =="
rg -n "RetiredProviderName|Sqlite" "$Root/src/CanDoItAll.Infrastructure/ControlPlane/LegacyDatabaseProfileCatalogQuarantine.cs"

Write-Host "== Hot switching/drain audit =="
rg -n "AcquireContextLeaseAsync|BeginSwitchAsync|WaitForDrainAsync|DatabaseContextLease|DatabaseSwitchSession|EnableMaintenanceHotSwitch" "$Root/src" "$Root/tests"

Write-Host "== Profile-specific context audit =="
rg -n "CreateDbContextForProfileAsync|ISwitchableAppDbContextFactory" "$Root/src" "$Root/tests"

Write-Host "== PostgreSQL claim audit =="
rg -n "FOR UPDATE SKIP LOCKED|LeaseToken|AutomationDispatchClaimToken|AutomationDispatchLeaseExpiresAtUtc" "$Root/src/CanDoItAll.Modules.*"
