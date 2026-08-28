param([Parameter(Mandatory)][ValidateSet('Unit', 'Components', 'Integration')][string] $Suite)
$ErrorActionPreference = 'Stop'
$project = "tests/$Suite/CanDoItAll.Tests.$Suite/CanDoItAll.Tests.$Suite.csproj"
$label = $Suite.ToLowerInvariant()
dotnet test $project --no-restore --list-tests -v quiet 2>&1 |
    Tee-Object -FilePath (Join-Path $PSScriptRoot "$label-broad-discovery.txt")
if ($LASTEXITCODE -ne 0) {
    throw "Broad discovery/build failed: $Suite"
}
dotnet test $project --no-build --no-restore --logger "trx;LogFileName=$label-broad.trx" --results-directory $PSScriptRoot -v quiet 2>&1 |
    Tee-Object -FilePath (Join-Path $PSScriptRoot "$label-broad.txt")
$testExit = $LASTEXITCODE
[xml] $result = Get-Content -LiteralPath (Join-Path $PSScriptRoot "$label-broad.trx")
if ([int] $result.TestRun.ResultSummary.Counters.total -le 0) {
    throw "Zero broad tests executed: $Suite"
}
Write-Output ($result.TestRun.ResultSummary.Counters | ConvertTo-Json -Compress)
exit $testExit
