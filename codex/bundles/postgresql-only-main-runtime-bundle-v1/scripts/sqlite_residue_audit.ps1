param(
    [string]$Root = "."
)

$patterns = @(
    "sqlite",
    "usesqlite",
    "migrations\.sqlite",
    "managedsqlite",
    "externalsqlite",
    "importedsqlite",
    "sqlitewritecoordination",
    "legacysqlitemigrationbootstrap",
    "snapshotcache",
    "ipfssnapshot"
)

foreach ($pattern in $patterns) {
    Write-Host "=== Pattern: $pattern ==="
    rg -n -i $pattern "$Root/src" "$Root/tests" "$Root/docs"
}
