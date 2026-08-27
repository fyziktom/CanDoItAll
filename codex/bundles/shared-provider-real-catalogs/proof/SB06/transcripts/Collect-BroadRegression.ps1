$ErrorActionPreference = 'Stop'
$proofRoot = Split-Path $PSScriptRoot -Parent
Write-Output 'Command: & proof/SB06/transcripts/Collect-BroadRegression.ps1'
foreach ($file in @('unit-suite.trx', 'integration-suite.trx')) {
    [xml] $run = Get-Content (Join-Path $proofRoot $file) -Raw
    $counters = $run.TestRun.ResultSummary.Counters
    Write-Output "Artifact: $file"
    Write-Output "Outcome: $($run.TestRun.ResultSummary.outcome)"
    Write-Output "Total: $($counters.total); Passed: $($counters.passed); Failed: $($counters.failed); NotExecuted: $($counters.notExecuted)"
    $skippedRecords = @($run.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'NotExecuted').Count
    Write-Output "Skipped result records: $skippedRecords; Aborted counter: $($counters.aborted)"
    foreach ($test in $run.TestRun.Results.UnitTestResult | Where-Object outcome -ne 'Passed') {
        Write-Output "$($test.outcome): $($test.testName)"
        $firstErrorLine = ([string] $test.Output.ErrorInfo.Message -split '[\r\n]+')[0]
        if ($firstErrorLine) {
            Write-Output "Reason: $firstErrorLine"
        }
    }
}
$unchangedOwners = @(
    'tests/Unit/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs',
    'tests/Integration/CanDoItAll.Tests.Integration/ManagedSeedExecutionFallbackIntegrationTests.cs',
    'tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs',
    'tests/Integration/CanDoItAll.Tests.Integration/PluginCatalogIntegrationTests.cs',
    'tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs',
    'tests/Integration/CanDoItAll.Tests.Integration/SharedProviderStreamingIntegrationTests.cs',
    'tests/Integration/CanDoItAll.Tests.Integration/SharedProviderBackendCheckpointIntegrationTests.cs',
    'src/MAF/Common/CanDoItAll.AgentFramework.Core/Agents/AgentDefinitionFactory.cs',
    'src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpRelayClient.cs',
    'src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpServiceCollectionExtensions.cs',
    'src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogProjection.cs'
)
foreach ($owner in $unchangedOwners) {
    if (-not (Test-Path -LiteralPath $owner)) {
        throw "Expected failure owner was not found: $owner"
    }
    git diff --exit-code -- $owner
    if ($LASTEXITCODE -ne 0) {
        throw "Failure owner has task changes: $owner"
    }
    Write-Output "Unchanged relative to HEAD: $owner"
}
Write-Output 'No pre-edit full-suite run was captured. Unchanged paths are not a claim of a measured pre-existing full-suite baseline.'
Write-Output 'Collector ExitCode: 0. This does not change the failing test-run outcomes above.'
