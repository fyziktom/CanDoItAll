param(
    [Parameter(Mandatory)] [string] $Project,
    [Parameter(Mandatory)] [string] $Filter,
    [Parameter(Mandatory)] [string] $RunLabel,
    [switch] $NoBuild,
    [int] $ExpectedCount = 0
)
$ErrorActionPreference = 'Stop'
$proofDirectory = $PSScriptRoot
$transcriptDirectory = Join-Path $proofDirectory 'transcripts'
New-Item -ItemType Directory -Path $transcriptDirectory -Force | Out-Null
Start-Transcript -Path (Join-Path $transcriptDirectory "$RunLabel.txt") -Force | Out-Null
try {
    Write-Output "SPMETA invariants: META-NAMES META-PRICES META-PRIVATE META-SETTINGS META-E2E"
    Write-Output "Working directory: $((Get-Location).Path)"
    if (-not $NoBuild) {
        Write-Output "dotnet build $Project --nologo"
        & dotnet build $Project --nologo | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw 'The selected test project did not build.'
        }
    }
    Write-Output "dotnet test $Project --no-build --list-tests --filter $Filter"
    $discovery = & dotnet test $Project --no-build --list-tests --filter $Filter
    $discovery | Write-Output
    if ($LASTEXITCODE -ne 0) {
        throw 'Test discovery failed.'
    }
    $discoveredCount = @($discovery | Where-Object { $_.Trim().StartsWith('CanDoItAll.Tests.') }).Count
    Write-Output "Discovered tests: $discoveredCount; expected: $ExpectedCount (0 means inspect the named selection)"
    if ($discoveredCount -eq 0 -or ($ExpectedCount -gt 0 -and $discoveredCount -ne $ExpectedCount)) {
        throw 'Unexpected test discovery count.'
    }
    $testArguments = @('test', $Project, '--filter', $Filter, '--logger', 'console;verbosity=normal', '--logger', "trx;LogFileName=$RunLabel.trx", '--results-directory', $transcriptDirectory, '--nologo')
    $testArguments += '--no-build'
    Write-Output "dotnet $($testArguments -join ' ')"
    & dotnet @testArguments | Out-Host
    $testExitCode = $LASTEXITCODE
    Write-Output "Exit code: $testExitCode"
} finally {
    Stop-Transcript | Out-Null
}
exit $testExitCode
