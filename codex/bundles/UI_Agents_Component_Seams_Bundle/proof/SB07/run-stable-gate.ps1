$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
if (-not (Test-Path -LiteralPath (Join-Path $repository 'CanDoItAll.slnx'))) {
    throw "Unexpected repository: $repository"
}
Set-Location -LiteralPath $repository
$transcripts = Join-Path $PSScriptRoot 'transcripts'
$stableFilter = 'Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true'
$commands = @(
    @{ Label = 'solution-restore'; Arguments = @('restore', 'CanDoItAll.slnx') },
    @{ Label = 'solution-build'; Arguments = @('build', 'CanDoItAll.slnx', '-c', 'Release', '--no-restore', '/m:1') },
    @{ Label = 'stable-restore'; Arguments = @('restore', 'tests/Solutions/CanDoItAll.Tests.Stable.slnx') },
    @{ Label = 'stable-build'; Arguments = @('build', 'tests/Solutions/CanDoItAll.Tests.Stable.slnx', '-c', 'Release', '--no-restore', '/m:1') },
    @{ Label = 'stable-discovery'; Arguments = @('test', 'tests/Solutions/CanDoItAll.Tests.Stable.slnx', '-c', 'Release', '--no-build', '--no-restore', '--list-tests', '--filter', $stableFilter, '/m:1') },
    @{ Label = 'stable-results'; Arguments = @('test', 'tests/Solutions/CanDoItAll.Tests.Stable.slnx', '-c', 'Release', '--no-build', '--no-restore', '--verbosity', 'normal', '--filter', $stableFilter, '/m:1') }
)
foreach ($command in $commands) {
    $log = Join-Path $transcripts ('final-' + $command.Label + '.log')
    @('SB07 INV-COMPOSITION INV-STATE INV-SESSION INV-WRITE', "WorkingDirectory=$repository", "RunLabel=$($command.Label)", "Started=$(Get-Date -Format o)", ('Command=dotnet ' + ($command.Arguments -join ' '))) | Set-Content -LiteralPath $log
    $arguments = $command.Arguments
    & dotnet @arguments *>> $log
    $result = $LASTEXITCODE
    "ExitCode=$result" | Add-Content -LiteralPath $log
    Write-Output "$($command.Label): exit $result"
    if ($result -ne 0) {
        Get-Content -LiteralPath $log -Tail 35
        exit $result
    }
    if ($command.Label -eq 'stable-discovery') {
        $names = @(Get-Content -LiteralPath $log | Where-Object { $_ -match '^    CanDoItAll\.' })
        if ($names.Count -eq 0) {
            throw 'Stable discovery returned no named cases.'
        }
        $names | Set-Content -LiteralPath (Join-Path $transcripts 'final-stable-expected-cases.txt')
        Write-Output "Stable named cases frozen before execution: $($names.Count)"
    }
}
