param([Parameter(Mandatory)][ValidateSet('Unit', 'Components', 'Integration')][string] $Suite)
$ErrorActionPreference = 'Stop'
$project = "tests/$Suite/CanDoItAll.Tests.$Suite/CanDoItAll.Tests.$Suite.csproj"
$label = $Suite.ToLowerInvariant() + '-broad'
$directory = Join-Path $PSScriptRoot 'transcripts'
Start-Transcript -Path (Join-Path $directory "$label.txt")
try {
    Write-Output "SB09 frozen checkpoint; required AllSuppliedSuites due TIA3001/TIA3004 unresolved dispatch; directory: $((Get-Location).Path)"
    Write-Output "dotnet test $project --no-build --no-restore --list-tests -v quiet"
    dotnet test $project --no-build --no-restore --list-tests -v quiet 2>&1 |
        Out-File (Join-Path $directory "$label-discovery.txt")
    if ($LASTEXITCODE -ne 0) {
        throw "Broad discovery failed: $Suite"
    }
    Write-Output "dotnet test $project --no-build --no-restore --logger trx;LogFileName=$label.trx --results-directory $directory -v quiet"
    dotnet test $project --no-build --no-restore --logger "trx;LogFileName=$label.trx" --results-directory $directory -v quiet
    $taskExit = $LASTEXITCODE
    Write-Output "Exit code: $taskExit"
    [xml]$result = Get-Content -LiteralPath (Join-Path $directory "$label.trx")
    Write-Output ($result.TestRun.ResultSummary.Counters | ConvertTo-Json -Compress)
    if ([int]$result.TestRun.ResultSummary.Counters.total -le 0) {
        throw 'Zero broad tests executed.'
    }
    exit $taskExit
} finally {
    Stop-Transcript
}
