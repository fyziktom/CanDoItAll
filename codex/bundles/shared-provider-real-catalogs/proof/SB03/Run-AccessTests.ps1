param(
    [Parameter(Mandatory)] [string] $Project,
    [Parameter(Mandatory)] [string] $Filter,
    [Parameter(Mandatory)] [string] $Label,
    [Parameter(Mandatory)] [int] $ExpectedCount,
    [switch] $NoBuild
)
$ErrorActionPreference = 'Stop'
$transcripts = Join-Path $PSScriptRoot 'transcripts'
New-Item -ItemType Directory -Path $transcripts -Force | Out-Null
Start-Transcript -Path (Join-Path $transcripts "$Label.txt") -Force | Out-Null
$result = 1
try {
    Write-Output 'Invariants: LOCAL-UI-ACCESS API-BOUNDARY'
    if (-not $NoBuild) {
        Write-Output "Command: dotnet build $Project --no-restore --nologo -v:q"
        & dotnet build $Project --no-restore --nologo -v:q | Out-Host
        Write-Output "Exit code: $LASTEXITCODE"
        if ($LASTEXITCODE -ne 0) {
            throw 'Build failed.'
        }
    }
    Write-Output "Command: dotnet test $Project --no-build --list-tests --filter $Filter"
    $discovery = & dotnet test $Project --no-build --list-tests --filter $Filter
    $discovery | Write-Output
    if ($LASTEXITCODE -ne 0) {
        throw 'Discovery failed.'
    }
    $count = @($discovery | Where-Object { $_.Trim().StartsWith('CanDoItAll.Tests.') }).Count
    Write-Output "Expected: $ExpectedCount; discovered: $count"
    if ($count -eq 0 -or $count -ne $ExpectedCount) {
        throw 'Unexpected discovery.'
    }
    $arguments = @('test', $Project, '--no-build', '--filter', $Filter,
        '--logger', 'console;verbosity=normal', '--logger', "trx;LogFileName=$Label.trx",
        '--results-directory', $transcripts, '--nologo')
    Write-Output "Command: dotnet $($arguments -join ' ')"
    & dotnet @arguments | Out-Host
    $result = $LASTEXITCODE
    Write-Output "Exit code: $result"
} finally {
    Stop-Transcript | Out-Null
}
exit $result
