param(
    [string]$RepositoryRoot,
    [string]$AssemblyPath = 'tests/Unit/CanDoItAll.Tests.Unit/bin/Release/net10.0/CanDoItAll.Tests.Unit.dll',
    [string]$ResultsDirectory = 'artifacts/unix-portability/B06/windows',
    [string]$TrxFileName = 'b06-unit-windows.trx'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
. (Join-Path $PSScriptRoot 'b06_focused_test_selection.ps1')
$selection = Get-B06FocusedUnitTestSelection -RepositoryRoot $RepositoryRoot

$resolvedAssemblyPath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $AssemblyPath))
if (-not (Test-Path -LiteralPath $resolvedAssemblyPath -PathType Leaf)) {
    throw "Build the Release unit-test assembly first: $AssemblyPath"
}

$resolvedResultsDirectory = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $ResultsDirectory))
[System.IO.Directory]::CreateDirectory($resolvedResultsDirectory) | Out-Null

& dotnet test $resolvedAssemblyPath `
    --filter $selection.Filter `
    --results-directory $resolvedResultsDirectory `
    --logger "trx;LogFileName=$TrxFileName"
if ($LASTEXITCODE -ne 0) {
    throw "The B06 focused unit-test slice failed with exit code $LASTEXITCODE."
}
