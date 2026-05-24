# SQLite residue audit for follow-up

$ErrorActionPreference = "Stop"

$patterns = "sqlite|usesqlite|migrations\.sqlite|managedsqlite|externalsqlite|importedsqlite|snapshotcache|ipfssnapshot|sqlitewritecoordination"

Write-Host "Running SQLite residue audit in src/tests/solution..."
$matches = & rg -n -i $patterns src tests CanDoItAll.slnx 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Unexpected SQLite residue found:"
    Write-Host $matches
    exit 1
}

if ($LASTEXITCODE -eq 1) {
    Write-Host "No SQLite residue found in runtime source/test scope."
    exit 0
}

Write-Host "ripgrep failed with code $LASTEXITCODE"
exit $LASTEXITCODE
