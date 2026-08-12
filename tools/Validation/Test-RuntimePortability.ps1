param(
    [string]$RepositoryRoot,
    [string]$Configuration = 'Release',
    [string]$ResultsDirectory,
    [bool]$UseLocalCanDoItAllLibraries = $true,
    [ValidateSet('All', 'Unit', 'Integration', 'Browser')]
    [string]$Scope = 'All'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $RepositoryRoot 'artifacts/runtime-portability'
}

$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
[System.IO.Directory]::CreateDirectory($ResultsDirectory) | Out-Null

$expectedUnitClasses = @(
    'CanDoItAll.Tests.Unit.AgentFrameworkHostingServiceCollectionTests',
    'CanDoItAll.Tests.Unit.AgentFrameworkProcessExecutionClaimRecoveryCoordinatorTests',
    'CanDoItAll.Tests.Unit.AgentFrameworkWorkspaceProcessLeaseCleanupTests',
    'CanDoItAll.Tests.Unit.ApplicationStoragePortabilityContractTests',
    'CanDoItAll.Tests.Unit.DotNetSolutionSetupRuntimeExecutorTests',
    'CanDoItAll.Tests.Unit.ProcessBlockedRunPersistedRecoveryTests',
    'CanDoItAll.Tests.Unit.ProcessesModuleHostedWorkerRegistrationTests',
    'CanDoItAll.Tests.Unit.ProcessLaunchExecutorResolverTests',
    'CanDoItAll.Tests.Unit.ProcessLaunchPromptTests',
    'CanDoItAll.Tests.Unit.ProcessRuntimeArchitectureBaselineTests',
    'CanDoItAll.Tests.Unit.ProcessRuntimeIntegrationAdapterTests',
    'CanDoItAll.Tests.Unit.WorkspaceRuntimePluginScriptArgumentTests'
)
$expectedIntegrationClasses = @(
    'CanDoItAll.Tests.Integration.CapsuleCatalogServiceIntegrationTests',
    'CanDoItAll.Tests.Integration.ExecutionFoundationPortabilityIntegrationTests',
    'CanDoItAll.Tests.Integration.ManagerProcessDiscoveryIntegrationTests',
    'CanDoItAll.Tests.Integration.McpExternalToolPortabilityIntegrationTests',
    'CanDoItAll.Tests.Integration.PluginDesktopPortabilityIntegrationTests',
    'CanDoItAll.Tests.Integration.ProcessCapabilityPortabilityIntegrationTests',
    'CanDoItAll.Tests.Integration.WatchSupervisorServiceIntegrationTests'
)
$expectedBrowserClasses = @(
    'CanDoItAll.Tests.Playwright.AppSmokeTests'
)

function Invoke-RuntimePortabilityProject {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,
        [Parameter(Mandatory)]
        [string]$TrxFileName
    )

    $arguments = @(
        'test',
        (Join-Path $RepositoryRoot $ProjectPath),
        '--configuration', $Configuration,
        '--no-build',
        '--no-restore',
        '--nologo',
        '--filter', 'Category=UnixRuntimePortability',
        '--logger', "trx;LogFileName=$TrxFileName",
        '--results-directory', $ResultsDirectory,
        "-p:UseLocalCanDoItAllLibraries=$($UseLocalCanDoItAllLibraries.ToString().ToLowerInvariant())"
    )

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime portability tests failed for '$ProjectPath' with exit code $LASTEXITCODE."
    }
}

function Assert-RuntimePortabilityTrx {
    param(
        [Parameter(Mandatory)]
        [string]$TrxFileName,
        [Parameter(Mandatory)]
        [string[]]$ExpectedClasses,
        [Parameter(Mandatory)]
        [int]$ExpectedCaseCount,
        [string[]]$ExpectedMethods = @()
    )

    $trxPath = Join-Path $ResultsDirectory $TrxFileName
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw "Runtime portability TRX was not produced: $trxPath"
    }

    [xml]$trx = Get-Content -LiteralPath $trxPath
    $namespace = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
    $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $summary = $trx.SelectSingleNode('//t:ResultSummary/t:Counters', $namespace)
    $actualCount = [int]$summary.total
    $failedCount = [int]$summary.failed + [int]$summary.error + [int]$summary.timeout + [int]$summary.aborted
    if ($actualCount -ne $ExpectedCaseCount -or $failedCount -ne 0) {
        throw "Runtime portability TRX '$TrxFileName' expected $ExpectedCaseCount passing cases; total=$actualCount failed=$failedCount."
    }

    $testMethods = @($trx.SelectNodes('//t:UnitTest/t:TestMethod', $namespace))
    $actualClasses = @($testMethods | ForEach-Object { $_.className } | Sort-Object -Unique)
    $missingClasses = @($ExpectedClasses | Where-Object { $_ -notin $actualClasses })
    $unexpectedClasses = @($actualClasses | Where-Object { $_ -notin $ExpectedClasses })
    if ($missingClasses.Count -ne 0 -or $unexpectedClasses.Count -ne 0) {
        throw "Runtime portability TRX '$TrxFileName' class selection drifted. Missing=[$($missingClasses -join ', ')] Unexpected=[$($unexpectedClasses -join ', ')]"
    }

    if ($ExpectedMethods.Count -ne 0) {
        $actualMethods = @($testMethods | ForEach-Object { $_.name } | Sort-Object -Unique)
        $missingMethods = @($ExpectedMethods | Where-Object { $_ -notin $actualMethods })
        $unexpectedMethods = @($actualMethods | Where-Object { $_ -notin $ExpectedMethods })
        if ($missingMethods.Count -ne 0 -or $unexpectedMethods.Count -ne 0) {
            throw "Runtime portability TRX '$TrxFileName' method selection drifted. Missing=[$($missingMethods -join ', ')] Unexpected=[$($unexpectedMethods -join ', ')]"
        }
    }
}

if ($Scope -in @('All', 'Unit')) {
    Invoke-RuntimePortabilityProject `
        -ProjectPath 'tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' `
        -TrxFileName 'runtime-portability-unit.trx'
    Assert-RuntimePortabilityTrx `
        -TrxFileName 'runtime-portability-unit.trx' `
        -ExpectedClasses $expectedUnitClasses `
        -ExpectedCaseCount 422
}

if ($Scope -in @('All', 'Integration')) {
    Invoke-RuntimePortabilityProject `
        -ProjectPath 'tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' `
        -TrxFileName 'runtime-portability-integration.trx'
    Assert-RuntimePortabilityTrx `
        -TrxFileName 'runtime-portability-integration.trx' `
        -ExpectedClasses $expectedIntegrationClasses `
        -ExpectedCaseCount 33
}

if ($Scope -in @('All', 'Browser')) {
    Invoke-RuntimePortabilityProject `
        -ProjectPath 'tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj' `
        -TrxFileName 'runtime-portability-browser.trx'
    Assert-RuntimePortabilityTrx `
        -TrxFileName 'runtime-portability-browser.trx' `
        -ExpectedClasses $expectedBrowserClasses `
        -ExpectedCaseCount 1 `
        -ExpectedMethods @('Runtime_node_actions_show_direct_optional_and_dependency_missing_states')
}

Write-Host "Runtime portability gate passed. Evidence: $ResultsDirectory"
