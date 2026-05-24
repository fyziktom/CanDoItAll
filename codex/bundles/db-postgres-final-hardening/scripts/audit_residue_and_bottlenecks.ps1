# Audit residue and bottleneck candidates
$ErrorActionPreference = "Stop"

$patterns = @(
    "UseSqlite",
    "Migrations.Sqlite",
    "SqliteWriteCoordination",
    "AcquireContextLeaseAsync",
    "BeginSwitchAsync",
    "WaitForDrainAsync",
    "DatabaseSwitchSession",
    "SaveChangesAsync\(cancellationToken\).*LeaseToken",
    "LeaseToken.*SaveChangesAsync"
)

foreach ($pattern in $patterns) {
    Write-Host "=== $pattern ==="
    rg -n -i $pattern src tests CanDoItAll.slnx
}
