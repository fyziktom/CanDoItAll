param(
    [Parameter(Mandatory)] [string] $Project,
    [Parameter(Mandatory)] [string] $Filter,
    [Parameter(Mandatory)] [string] $Label
)
$ErrorActionPreference = 'Stop'
$proofDirectory = Join-Path $PSScriptRoot 'transcripts'
Start-Transcript -Path (Join-Path $proofDirectory "$Label.txt")
try {
    Write-Output "SB09-I1/I2/I3; directory: $((Get-Location).Path); checkpoint: $Label"
    Write-Output "dotnet test $Project --no-restore --list-tests --filter $Filter -v quiet"
    dotnet test $Project --no-restore --list-tests --filter $Filter -v quiet 2>&1 |
        Tee-Object -FilePath (Join-Path $proofDirectory "$Label-discovery.txt")
    if ($LASTEXITCODE -ne 0) {
        throw "Discovery/build failed with exit $LASTEXITCODE."
    }
    Write-Output "dotnet test $Project --no-build --no-restore --filter $Filter --logger trx;LogFileName=$Label.trx --results-directory $proofDirectory -v quiet"
    dotnet test $Project --no-build --no-restore --filter $Filter --logger "trx;LogFileName=$Label.trx" --results-directory $proofDirectory -v quiet
    $taskTestExit = $LASTEXITCODE
    Write-Output "Exit code: $taskTestExit"
    [xml]$result = Get-Content -LiteralPath (Join-Path $proofDirectory "$Label.trx")
    $total = [int]$result.TestRun.ResultSummary.Counters.total
    if ($total -le 0) {
        throw 'Zero executed tests cannot satisfy this gate.'
    }
    Write-Output "Executed $total cases; compare exact test identities with discovery."
    exit $taskTestExit
} finally {
    Stop-Transcript
}
