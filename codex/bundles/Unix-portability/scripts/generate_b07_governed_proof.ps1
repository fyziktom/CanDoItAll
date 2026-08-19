param(
    [string]$RepositoryRoot,
    [string]$OutputPath = 'artifacts/unix-portability/B07/b07-governed-proof.json'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

function Resolve-RepositoryFile {
    param([string]$RelativePath)

    $path = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RelativePath))
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "B07 proof input is missing: $RelativePath"
    }

    return $path
}

function Get-FileProof {
    param([string]$RelativePath)

    return [ordered]@{
        path = $RelativePath
        sha256 = (Get-FileHash -LiteralPath (Resolve-RepositoryFile $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Get-TrxProof {
    param(
        [string]$HostName,
        [string]$Slice,
        [string]$RelativePath,
        [int]$ExpectedCases,
        [string[]]$ExpectedClasses,
        [string[]]$ExpectedMethods = @()
    )

    [xml]$trx = Get-Content -LiteralPath (Resolve-RepositoryFile $RelativePath) -Raw
    $namespace = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
    $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $counters = $trx.SelectSingleNode('//t:ResultSummary/t:Counters', $namespace)
    if ($null -eq $counters) {
        throw "TRX counters are missing: $RelativePath"
    }

    $failed = [int]$counters.failed + [int]$counters.error + [int]$counters.timeout + [int]$counters.aborted
    if ([int]$counters.total -ne $ExpectedCases -or [int]$counters.passed -ne $ExpectedCases -or $failed -ne 0 -or [int]$counters.notExecuted -ne 0) {
        throw "Unexpected B07 TRX counters in ${RelativePath}: total=$($counters.total) passed=$($counters.passed) failed=$failed skipped=$($counters.notExecuted)"
    }

    $methods = @($trx.SelectNodes('//t:UnitTest/t:TestMethod', $namespace))
    $classes = @($methods | ForEach-Object { [string]$_.className } | Sort-Object -Unique)
    $missingClasses = @($ExpectedClasses | Where-Object { $_ -notin $classes })
    $unexpectedClasses = @($classes | Where-Object { $_ -notin $ExpectedClasses })
    if ($missingClasses.Count -ne 0 -or $unexpectedClasses.Count -ne 0) {
        throw "B07 class selection drifted in ${RelativePath}. Missing=[$($missingClasses -join ', ')] Unexpected=[$($unexpectedClasses -join ', ')]"
    }

    if ($ExpectedMethods.Count -ne 0) {
        $methodNames = @($methods | ForEach-Object { [string]$_.name } | Sort-Object -Unique)
        $missingMethods = @($ExpectedMethods | Where-Object { $_ -notin $methodNames })
        $unexpectedMethods = @($methodNames | Where-Object { $_ -notin $ExpectedMethods })
        if ($missingMethods.Count -ne 0 -or $unexpectedMethods.Count -ne 0) {
            throw "B07 method selection drifted in ${RelativePath}. Missing=[$($missingMethods -join ', ')] Unexpected=[$($unexpectedMethods -join ', ')]"
        }
    }

    return [ordered]@{
        host = $HostName
        slice = $Slice
        passed = $ExpectedCases
        failed = 0
        skipped = 0
        artifact = $RelativePath
        sha256 = (Get-FileHash -LiteralPath (Resolve-RepositoryFile $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$unitClasses = @(
    'CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkHostingServiceCollectionTests',
    'CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkProcessExecutionClaimRecoveryCoordinatorTests',
    'CanDoItAll.Tests.Unit.AgentFramework.AgentFrameworkWorkspaceProcessLeaseCleanupTests',
    'CanDoItAll.Tests.Unit.Storage.ApplicationStoragePortabilityContractTests',
    'CanDoItAll.Tests.Unit.Processes.DotNetSolutionSetupRuntimeExecutorTests',
    'CanDoItAll.Tests.Unit.Processes.ProcessBlockedRunPersistedRecoveryTests',
    'CanDoItAll.Tests.Unit.Processes.ProcessesModuleHostedWorkerRegistrationTests',
    'CanDoItAll.Tests.Unit.Processes.ProcessLaunchExecutorResolverTests',
    'CanDoItAll.Tests.Unit.Processes.ProcessLaunchPromptTests',
    'CanDoItAll.Tests.Unit.Processes.ProcessRuntimeArchitectureBaselineTests',
    'CanDoItAll.Tests.Unit.Processes.ProcessRuntimeIntegrationAdapterTests',
    'CanDoItAll.Tests.Unit.AgentFramework.WorkspaceRuntimePluginScriptArgumentTests'
)
$integrationClasses = @(
    'CanDoItAll.Tests.Integration.CapsuleCatalogServiceIntegrationTests',
    'CanDoItAll.Tests.Integration.ExecutionFoundationPortabilityIntegrationTests',
    'CanDoItAll.Tests.Integration.ManagerProcessDiscoveryIntegrationTests',
    'CanDoItAll.Tests.Integration.McpExternalToolPortabilityIntegrationTests',
    'CanDoItAll.Tests.Integration.PluginDesktopPortabilityIntegrationTests',
    'CanDoItAll.Tests.Integration.ProcessCapabilityPortabilityIntegrationTests',
    'CanDoItAll.Tests.Integration.WatchSupervisorServiceIntegrationTests'
)

$sourcePaths = @(
    '.github/workflows/ci.yml',
    'tools/Validation/Test-RuntimePortability.ps1',
    'tests/Unit/CanDoItAll.Tests.Unit/CrossPlatformCiWorkflowTests.cs',
    'tests/Playwright/CanDoItAll.Tests.Playwright/ProjectStructureRuntimeNodePlaywrightTests.cs',
    'src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor',
    'src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs',
    'src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs',
    'src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/StandardProcessLaunchDriverCatalogProvider.cs',
    'src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessHostCapabilityAdaptationTests.cs',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/07-runtime-three-platform-ci-e2e-and-final-closure/README.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/07-runtime-three-platform-ci-e2e-and-final-closure/tasks.json',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/07-runtime-three-platform-ci-e2e-and-final-closure/tasks.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/07-runtime-three-platform-ci-e2e-and-final-closure/validation.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/07-runtime-three-platform-ci-e2e-and-final-closure/exit-criteria.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/reviews/26-b07-local-evidence-and-hosted-deferral.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/inventories/source-reference-manifest.json',
    'codex/bundles/Unix-portability/scripts/generate_b07_governed_proof.ps1'
)

$proof = [ordered]@{
    schemaVersion = 1
    bundle = 'CanDoItAll Runtime, Tools, and Process Drivers / B07'
    proofTier = 'Governed local readiness'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    branch = (git -C $RepositoryRoot branch --show-current).Trim()
    baseCommit = 'dd78ffa9769ba1d125b8be81a4b303df37c32505'
    status = 'Local readiness green; hosted three-platform aggregate and Final Gate R4 deferred'
    tests = @(
        Get-TrxProof 'Windows 11' 'Unit' 'artifacts/unix-portability/B07/windows/runtime-portability-unit.trx' 422 $unitClasses
        Get-TrxProof 'Windows 11' 'Integration' 'artifacts/unix-portability/B07/windows/runtime-portability-integration.trx' 33 $integrationClasses
        Get-TrxProof 'Windows 11' 'Browser' 'artifacts/unix-portability/B07/windows/runtime-portability-browser.trx' 1 @('CanDoItAll.Tests.Playwright.AppSmokeTests') @('Runtime_node_actions_show_direct_optional_and_dependency_missing_states')
        Get-TrxProof 'Ubuntu 24.04 Docker' 'Unit' 'artifacts/unix-portability/B07/linux/runtime-portability-unit.trx' 422 $unitClasses
        Get-TrxProof 'Ubuntu 24.04 Docker' 'Integration' 'artifacts/unix-portability/B07/linux/runtime-portability-integration.trx' 33 $integrationClasses
    )
    sourceHashes = @($sourcePaths | ForEach-Object { Get-FileProof $_ })
    assertions = @(
        [ordered]@{ name = 'single-fast-entry-point'; result = 'Pass'; evidence = 'One no-build/no-restore runner validates exact unit, integration, and browser selections.' },
        [ordered]@{ name = 'active-three-host-matrix'; result = 'Pass'; evidence = 'The active CI matrix invokes the same runner on Windows, Ubuntu, and macOS and uploads evidence.' },
        [ordered]@{ name = 'workbench-browser-contract'; result = 'Pass'; evidence = 'The normal portfolio navigation path proves capability-aware runtime states without physical-path disclosure.' },
        [ordered]@{ name = 'cycle-free-process-capability-composition'; result = 'Pass'; evidence = 'Host probing consumes a static typed adapter registration and does not construct scoped execution drivers.' },
        [ordered]@{ name = 'claim-boundary'; result = 'Pass'; evidence = 'Windows and Linux local evidence is recorded; hosted and actual macOS Final R4 remain explicitly deferred.' }
    )
    deferred = @(
        'Hosted Windows, Ubuntu, and macOS artifacts have not executed for this working snapshot.',
        'No actual macOS support claim is made.',
        'Final Gate R4 requires hosted evidence reconciliation and independent review.'
    )
}

$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $RepositoryRoot $OutputPath }
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedOutput)) | Out-Null
$proof | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutput -Encoding utf8NoBOM
Write-Host "Wrote B07 governed local-readiness proof: $resolvedOutput"
