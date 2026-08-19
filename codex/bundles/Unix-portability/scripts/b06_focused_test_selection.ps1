$script:B06FocusedTestBaseCommit = 'dd78ffa9769ba1d125b8be81a4b303df37c32505'
$script:B06FocusedUnitTestMethodCount = 124
$script:B06FocusedUnitTestCaseCount = 206
$script:B06FocusedUnitTestFiles = @(
    'tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessHostCapabilityAdaptationTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchExecutorResolverTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRequiredRuntimeToolNamesTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessInstancePlanCompilerTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessModuleBoundaryTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeOperatorApplicationServiceTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeToolPreflightServiceTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/RuntimeHostPlatformCapabilityTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/WorkspaceProductFilesystemCompletionGateContributionTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessProductRootResolverPortabilityTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessExecutionAdapterBoundaryTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessDriverAbstractionTests.cs'
)

function Get-B06FocusedUnitTestSelection {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $diff = @(& git -C $resolvedRoot diff --unified=0 $script:B06FocusedTestBaseCommit -- $script:B06FocusedUnitTestFiles)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to derive the B06 focused unit-test selection from the governed source diff.'
    }

    $methodNames = @($diff |
        Select-String -Pattern '^\+\s*public\s+(?:async\s+)?(?:Task|ValueTask|void)\s+([A-Za-z0-9_]+)' |
        ForEach-Object { $_.Matches[0].Groups[1].Value } |
        Where-Object { $_ -ne 'Dispose' } |
        Sort-Object -Unique)

    if ($methodNames.Count -ne $script:B06FocusedUnitTestMethodCount) {
        throw "The governed B06 selection changed: expected $script:B06FocusedUnitTestMethodCount added methods, found $($methodNames.Count)."
    }

    [pscustomobject]@{
        BaseCommit = $script:B06FocusedTestBaseCommit
        Files = $script:B06FocusedUnitTestFiles
        MethodNames = $methodNames
        ExpectedMethodCount = $script:B06FocusedUnitTestMethodCount
        ExpectedCaseCount = $script:B06FocusedUnitTestCaseCount
        Filter = '(' + (($methodNames | ForEach-Object { "FullyQualifiedName~$_" }) -join '|') + ')'
    }
}
