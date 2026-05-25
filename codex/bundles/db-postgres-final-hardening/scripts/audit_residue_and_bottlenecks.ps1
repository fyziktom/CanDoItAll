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
    $matches = rg -n -i $pattern src tests CanDoItAll.slnx
    if ($LASTEXITCODE -eq 0) {
        $matches
        continue
    }

    if ($LASTEXITCODE -eq 1) {
        Write-Host "(no matches)"
        continue
    }

    throw "rg failed for pattern '$pattern' with exit code $LASTEXITCODE."
}
