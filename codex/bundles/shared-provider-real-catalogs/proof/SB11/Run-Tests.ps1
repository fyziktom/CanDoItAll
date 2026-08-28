param(
    [Parameter(Mandatory)][string] $Label,
    [string] $Project = 'tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj',
    [switch] $NoBuild,
    [string] $Filter = 'FullyQualifiedName~AgentProviderFailureDisplayFormatterTests|FullyQualifiedName~MafProviderTransportBoundaryChatClientTests|FullyQualifiedName~MafWorkflowExecutorFailureDiagnosticsTests'
)
$ErrorActionPreference = 'Stop'
$taskProject = $Project
$taskBuildArguments = @()
if ($NoBuild) {
    $taskBuildArguments = @('--no-build')
}
$taskOutput = Join-Path $PSScriptRoot 'transcripts'
Start-Transcript -Path (Join-Path $taskOutput ($Label + '.txt')) -Force
try {
    Write-Output ('SB11 regression gate; directory: ' + (Get-Location).Path + '; NoBuild=' + $NoBuild)
    Write-Output ('dotnet test ' + $taskProject + ' --no-restore ' + ($taskBuildArguments -join ' ') + ' --list-tests --filter "' + $Filter + '" -v quiet')
    $taskDiscovery = dotnet test $taskProject --no-restore @taskBuildArguments --list-tests --filter $Filter -v quiet
    $taskDiscovery | Out-File (Join-Path $taskOutput ($Label + '-discovery.txt'))
    if ($LASTEXITCODE -ne 0) {
        throw 'Discovery/build failed.'
    }
    $taskNames = @($taskDiscovery | ForEach-Object { $_.Trim() } | Where-Object { $_ -like 'CanDoItAll.Tests.*' })
    if ($taskNames.Count -eq 0) {
        throw 'Zero discovered tests.'
    }
    Write-Output ('Frozen discovered cases: ' + $taskNames.Count)
    Write-Output ('dotnet test ' + $taskProject + ' --no-build --no-restore --filter "' + $Filter + '" --logger "trx;LogFileName=' + $Label + '.trx" --results-directory ' + $taskOutput + ' -v quiet')
    dotnet test $taskProject --no-build --no-restore --filter $Filter --logger ('trx;LogFileName=' + $Label + '.trx') --results-directory $taskOutput -v quiet
    $taskTestExit = $LASTEXITCODE
    [xml] $taskTrx = Get-Content -Raw (Join-Path $taskOutput ($Label + '.trx'))
    $taskActual = @($taskTrx.TestRun.Results.UnitTestResult | ForEach-Object testName)
    $taskDifferences = @(Compare-Object $taskNames $taskActual -CaseSensitive)
    if ($taskDifferences.Count -ne 0) {
        $taskDeferred = @($taskDifferences | Where-Object SideIndicator -eq '<=' | ForEach-Object InputObject)
        $taskExpanded = @($taskDifferences | Where-Object SideIndicator -eq '=>' | ForEach-Object InputObject)
        foreach ($taskName in $taskDeferred) {
            if ($taskName.Contains('(') -or
                @($taskExpanded | Where-Object { $_.StartsWith($taskName + '(', [StringComparison]::Ordinal) }).Count -eq 0) {
                throw ('A frozen discovered test is missing: ' + $taskName)
            }
        }
        foreach ($taskName in $taskExpanded) {
            if (@($taskDeferred | Where-Object { $taskName.StartsWith($_ + '(', [StringComparison]::Ordinal) }).Count -ne 1) {
                throw ('An executed test does not match a discovered deferred theory: ' + $taskName)
            }
        }
        $taskDifferences | ConvertTo-Json | Out-File (Join-Path $taskOutput ($Label + '-deferred-theories.json'))
        Write-Output ('Validated runtime expansion: ' + $taskDeferred.Count + ' discovered deferred theories into ' + $taskExpanded.Count + ' cases. No missing or unselected tests.')
    }
    Write-Output ('Discovery/result identities reconciled. Test exit: ' + $taskTestExit)
    Write-Output ($taskTrx.TestRun.ResultSummary.Counters | ConvertTo-Json -Compress)
} finally {
    Stop-Transcript
}
exit $taskTestExit
