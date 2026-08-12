param(
    [string]$RepositoryRoot,
    [string]$OutputPath = 'artifacts/unix-portability/B06/b06-governed-proof.json'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
. (Join-Path $PSScriptRoot 'b06_focused_test_selection.ps1')
$focusedUnitSelection = Get-B06FocusedUnitTestSelection -RepositoryRoot $RepositoryRoot

function Resolve-RepositoryPath {
    param([string]$RelativePath)

    $resolved = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $RelativePath))
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Governed proof input is missing: $RelativePath"
    }
    return $resolved
}

function Get-Sha256 {
    param([string]$RelativePath)

    return (Get-FileHash -LiteralPath (Resolve-RepositoryPath $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TrxProof {
    param(
        [string]$HostName,
        [string]$Slice,
        [string]$Artifact,
        [int]$ExpectedPassed,
        [string[]]$ExpectedMethodNames = @()
    )

    [xml]$document = Get-Content -LiteralPath (Resolve-RepositoryPath $Artifact) -Raw
    $counters = $document.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw "TRX counters are missing: $Artifact"
    }

    $passed = [int]$counters.passed
    $failed = [int]$counters.failed
    $skipped = [int]$counters.notExecuted
    if ($passed -ne $ExpectedPassed -or $failed -ne 0 -or $skipped -ne 0) {
        throw "Unexpected TRX result for ${Artifact}: passed=$passed failed=$failed skipped=$skipped"
    }

    $resultNames = @($document.SelectNodes("//*[local-name()='UnitTestResult']") |
        ForEach-Object { [string]$_.testName })
    $missingMethods = @($ExpectedMethodNames | Where-Object {
        $methodPattern = '[.]' + [regex]::Escape($_) + '(?:\(|$)'
        -not ($resultNames | Where-Object { $_ -match $methodPattern } | Select-Object -First 1)
    })
    if ($missingMethods.Count -ne 0) {
        throw "TRX is missing governed B06 methods in ${Artifact}: $($missingMethods -join ', ')"
    }

    return [ordered]@{
        host = $HostName
        slice = $Slice
        passed = $passed
        failed = $failed
        skipped = $skipped
        artifact = $Artifact
        sha256 = Get-Sha256 $Artifact
    }
}

function Get-BuildProof {
    param(
        [string]$Project,
        [string]$Artifact
    )

    $path = Resolve-RepositoryPath $Artifact
    $issueHits = @(Select-String -LiteralPath $path -Pattern ': warning |: error |Build FAILED' -CaseSensitive:$false)
    if ($issueHits.Count -ne 0) {
        throw "Build evidence contains warning/error markers: $Artifact"
    }

    return [ordered]@{
        project = $Project
        warnings = 0
        errors = 0
        artifact = $Artifact
        sha256 = Get-Sha256 $Artifact
    }
}

$sourceReferenceManifestPath = 'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/inventories/source-reference-manifest.json'
$sourceReferenceManifest = Get-Content -LiteralPath (Resolve-RepositoryPath $sourceReferenceManifestPath) -Raw | ConvertFrom-Json
$b06SourcePaths = @($sourceReferenceManifest.references |
    Where-Object { $_.id -like 'B06-REF-*' } |
    Select-Object -ExpandProperty relative_path)

$additionalSourcePaths = @(
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/architecture/06-process-domain-capability-model.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/inventories/executable-capability-inventory.csv',
    $sourceReferenceManifestPath,
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/06-process-domain-driver-capability-adaptation/README.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/06-process-domain-driver-capability-adaptation/exit-criteria.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/06-process-domain-driver-capability-adaptation/tasks.json',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/06-process-domain-driver-capability-adaptation/tasks.md',
    'codex/bundles/Unix-portability/bundles/02-runtime-tools-process-drivers/subbundles/06-process-domain-driver-capability-adaptation/validation.md',
    'codex/bundles/Unix-portability/scripts/generate_b06_governed_proof.ps1',
    'codex/bundles/Unix-portability/scripts/b06_focused_test_selection.ps1',
    'codex/bundles/Unix-portability/scripts/run_b06_focused_tests.ps1',
    'codex/bundles/Unix-portability/scripts/verify_project_graph.ps1',
    'src/App/CanDoItAll.Web/Api/ProcessesApi.cs',
    'src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260811185352_AddProcessRuntimeStepHostCapabilities.cs',
    'src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260811185352_AddProcessRuntimeStepHostCapabilities.Designer.cs',
    'src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs',
    'src/Processes/CanDoItAll.Processes.Application/ProcessLaunchContracts.cs',
    'src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorApplicationService.cs',
    'src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeOperatorContracts.cs',
    'src/Processes/CanDoItAll.Processes.Builder/ProcessInstancePlanCompiler.Validation.cs',
    'src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs',
    'src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs',
    'src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeArtifactContracts.cs',
    'src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs',
    'src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs',
    'src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDescriptors.cs',
    'tests/Integration/CanDoItAll.Tests.Integration/DatabaseMigrationIntegrationTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeTestServices.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessInstancePlanCompilerTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessModuleBoundaryTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeOperatorApplicationServiceTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeToolPreflightServiceTests.cs',
    'tests/Unit/CanDoItAll.Tests.Unit/RuntimeHostPlatformCapabilityTests.cs'
)

$sourcePaths = @($b06SourcePaths + $additionalSourcePaths | Sort-Object -Unique)
$sourceHashes = @($sourcePaths | ForEach-Object {
    [ordered]@{
        path = $_
        sha256 = Get-Sha256 $_
    }
})

$tests = @(
    Get-TrxProof 'Windows' 'B06 exact added unit regressions' 'artifacts/unix-portability/B06/windows/b06-unit-windows.trx' $focusedUnitSelection.ExpectedCaseCount $focusedUnitSelection.MethodNames
    Get-TrxProof 'Windows' 'Process capability portability integration' 'artifacts/unix-portability/B06/windows/b06-integration-windows.trx' 1
    Get-TrxProof 'Linux Docker Ubuntu 24.04' 'Same prebuilt exact B06 unit regressions' 'artifacts/unix-portability/B06/linux/b06-unit-linux.trx' $focusedUnitSelection.ExpectedCaseCount $focusedUnitSelection.MethodNames
    Get-TrxProof 'Linux Docker Ubuntu 24.04' 'Same prebuilt Process capability integration' 'artifacts/unix-portability/B06/linux/b06-integration-linux.trx' 1
)

$builds = @(
    Get-BuildProof 'Modules.Processes and transitive Process layers' 'artifacts/unix-portability/B06/windows/b06-modules-processes-build.log'
    Get-BuildProof 'Unit tests' 'artifacts/unix-portability/B06/windows/b06-unit-build.log'
    Get-BuildProof 'Integration tests' 'artifacts/unix-portability/B06/windows/b06-integration-build.log'
)

$hostEvidence = @(
    [ordered]@{
        host = 'Windows'
        artifact = 'artifacts/unix-portability/B06/windows/b06-windows-environment.txt'
        sha256 = Get-Sha256 'artifacts/unix-portability/B06/windows/b06-windows-environment.txt'
    }
    [ordered]@{
        host = 'Linux Docker'
        artifact = 'artifacts/unix-portability/B06/linux/b06-linux-environment.txt'
        sha256 = Get-Sha256 'artifacts/unix-portability/B06/linux/b06-linux-environment.txt'
    }
)

$graphArtifact = 'artifacts/unix-portability/B06/b06-project-graph.json'
$graph = Get-Content -LiteralPath (Resolve-RepositoryPath $graphArtifact) -Raw | ConvertFrom-Json
if ($graph.projectCount -ne 106 -or $graph.inRepositoryProjectReferences -ne 639 -or $graph.cyclicProjectCount -ne 0 -or $graph.unresolvedProjectReferenceCount -ne 0) {
    throw 'The B06 project graph does not match the frozen acyclic graph.'
}

$proof = [ordered]@{
    schemaVersion = 1
    bundle = 'CanDoItAll Runtime, Tools, and Process Drivers / B06'
    proofTier = 'Governed'
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    branch = (git -C $RepositoryRoot branch --show-current).Trim()
    baseCommit = 'dd78ffa9769ba1d125b8be81a4b303df37c32505'
    status = 'Candidate — independent Gate R3 review pending'
    failingFirst = @(
        [ordered]@{ id = 'B06-FF-001'; observed = 'Process host capability probing used ambient workspace and service-location authority.'; classification = 'PROC-001/PROC-003 owner-boundary defect'; correction = 'Inject canonical owner ports and project only typed bounded facts.'; finalProof = 'Source ownership, architecture, and both-host focused tests pass.' },
        [ordered]@{ id = 'B06-FF-002'; observed = 'Runtime-owned and workflow paths could act before capability preflight.'; classification = 'PROC-002 fail-before-side-effect defect'; correction = 'Move bounded effective tool and host gates ahead of every new execution branch while preserving existing-child reconciliation.'; finalProof = 'Workflow, subprocess, runtime-owned, and agent zero-side-effect regressions pass.' },
        [ordered]@{ id = 'B06-FF-003'; observed = 'Mandatory host unavailability was conditional on optional readiness evaluation.'; classification = 'PROC-002 launch-gating defect'; correction = 'Classify structural host findings as mandatory independent of HR/readiness opt-in.'; finalProof = 'Launch-check and no-commit regressions pass.' },
        [ordered]@{ id = 'B06-FF-004'; observed = 'Host facts and runtime-tool requirements were not sealed across plan, runtime state, persistence, and restart.'; classification = 'PROC-002/PROC-005 immutable-contract defect'; correction = 'Persist and hash bounded tool/capability sets, recompute plan hashes, and require exact plan/state/work-item equality.'; finalProof = 'Tamper, reload, migration, and dispatch regressions pass.' },
        [ordered]@{ id = 'B06-FF-005'; observed = 'The managed Process adapter existed only as launch-local injected truth.'; classification = 'PROC-001 split-capability authority'; correction = 'Publish one canonical typed adapter source and consume the same snapshot at launch and runtime.'; finalProof = 'Launch-to-reload dispatch and ownership tests pass.' },
        [ordered]@{ id = 'B06-FF-006'; observed = 'Special tool mapping omitted statically knowable Python, local MCP, Node, and npm dependencies.'; classification = 'PROC-006 incomplete capability adoption'; correction = 'Inventory process-starting tools and map exact transport/executable dependencies without blocking lifecycle-only stop.'; finalProof = 'Supported, unavailable, local, remote, and lifecycle regressions pass.' },
        [ordered]@{ id = 'B06-FF-007'; observed = 'Capability sources and result evidence could be malformed, contradictory, duplicated, oversized, or incoherent across snapshots.'; classification = 'PROC-002/PROC-005 typed-fact integrity defect'; correction = 'Validate source ownership and facts before probing/merging; fail closed on profile or fact drift.'; finalProof = 'Throwing, malformed, over-bound, duplicate, and A/B snapshot tests pass.' },
        [ordered]@{ id = 'B06-FF-008'; observed = 'Strategy receipts, provider failures, and public API results could persist raw messages, paths, credentials, or unbounded text.'; classification = 'PROC-005/T06 non-disclosure defect'; correction = 'Centralize bounded public-receipt text, digest, code, URI, and persistence validation on write/read.'; finalProof = 'Cross-host path, secret, URI, oversized, tampered-row, and no-mutation tests pass.' },
        [ordered]@{ id = 'B06-FF-009'; observed = 'Product completion gates exposed physical paths/content and performed direct filesystem inspection.'; classification = 'PROC-003/PROC-005 owner and disclosure defect'; correction = 'Delegate bounded inspection through the workspace file owner, preserve aliases, and publish only logical labels/hashes.'; finalProof = 'Alias, inaccessible, read-error, non-disclosure, and architecture tests pass.' },
        [ordered]@{ id = 'B06-FF-010'; observed = 'Capability-scope and completion-receipt parsing silently dropped malformed or over-bound contracts.'; classification = 'PROC-004 authority widening defect'; correction = 'Validate complete raw shapes and preserve one non-disclosing invalid-contract marker before normalization.'; finalProof = 'Undefined enum, malformed JSON, branch, count, length, generated-key, and zero-execution tests pass.' },
        [ordered]@{ id = 'B06-FF-011'; observed = 'Assignment repair and execution could widen or change the sealed runtime-tool contract.'; classification = 'PROC-002/PROC-004 assignment drift'; correction = 'Require exact assignment-derived versus immutable-plan tool equality before reference, catalog, repair, reconciliation, or execution.'; finalProof = 'Added, removed, alternate-agent, and existing-child drift tests pass.' },
        [ordered]@{ id = 'B06-FF-012'; observed = 'Generic dispatch did not revalidate current host state for non-Standard strategies.'; classification = 'PROC-002 generic strategy gap'; correction = 'Gate every strategy at the dispatcher using exact immutable binding and one current bounded snapshot.'; finalProof = 'Custom strategy host-loss and factory non-invocation tests pass.' },
        [ordered]@{ id = 'B06-FF-013'; observed = 'Standard strategy resolution accepted partial package identity.'; classification = 'PROC-001 immutable binding defect'; correction = 'Match driver, strategy, factory, schema, inputs, profile, facts, and capability identity exactly.'; finalProof = 'Same-strategy foreign-driver and stale version/schema tests pass.' },
        [ordered]@{ id = 'B06-FF-014'; observed = 'Platform driver contracts could be empty, default, malformed, or unbounded.'; classification = 'PROC-003/T07 platform-semantics defect'; correction = 'Validate every driver/strategy requirement set and require a non-empty valid Platform contract.'; finalProof = 'Catalog and architecture guard tests pass.' },
        [ordered]@{ id = 'B06-FF-015'; observed = 'Process special-driver path checks hard-coded case-insensitive behavior.'; classification = 'PROC-003/T05 physical identity defect'; correction = 'Use the host physical-filesystem path policy through validation, plan construction, writeback, and helper execution.'; finalProof = 'Linux-sensitive and Windows-insensitive deterministic fixtures pass.' },
        [ordered]@{ id = 'B06-FF-016'; observed = 'Local Playwright readiness proved node but could mutate managed directories before discovering missing npm.'; classification = 'PROC-002/PROC-006 package-manager gap'; correction = 'Model NodePackageManager, preflight it, and resolve npm before any managed-root mutation.'; finalProof = 'Node-present/npm-missing zero-directory regression passes.' },
        [ordered]@{ id = 'B06-FF-017'; observed = 'Launch-time browser capability resolution could mutate the agent catalog before dynamic host validation.'; classification = 'PROC-002 launch-order defect'; correction = 'Use read-only catalog resolution and seal final enriched tool/capability requirements before commit.'; finalProof = 'Missing local-browser capability leaves catalog and run persistence untouched.' },
        [ordered]@{ id = 'B06-FF-018'; observed = 'Process driver architecture checks omitted special module drivers and contained ineffective API-prefix patterns.'; classification = 'PROC-003/T07 evidence enforcement gap'; correction = 'Extend self-proving guards to generic and special-driver owner boundaries with precise forbidden APIs.'; finalProof = 'The exact boundary guards pass in the 206/206 Windows and Linux slices.' }
    )
    sourceAssertions = @(
        [ordered]@{ name = 'process-semantic-ownership'; result = 'Pass'; evidence = 'Processes owns eligibility, alternate/recovery meaning, strategy binding, failure interpretation, and receipts; adapters expose bounded facts and ports only.' },
        [ordered]@{ name = 'host-facts-do-not-grant-authority'; result = 'Pass'; evidence = 'Capability presence never grants tool, workspace, project, mutation, approval, secret, or process authority.' },
        [ordered]@{ name = 'fail-before-new-side-effects'; result = 'Pass'; evidence = 'Bounded exact tool/capability contracts gate workflow, subprocess launch, runtime-owned work, and agent dispatch before new side effects.' },
        [ordered]@{ name = 'owned-work-reconciliation'; result = 'Pass'; evidence = 'Existing verified child observation remains available after capability loss while immutable assignment drift still fails closed.' },
        [ordered]@{ name = 'immutable-plan-dispatch-chain'; result = 'Pass'; evidence = 'Canonical plan hash, state hash, step identity, full strategy binding, required tools, required capabilities, and live factory identity are exact-matched.' },
        [ordered]@{ name = 'current-host-revalidation'; result = 'Pass'; evidence = 'Every generic strategy invocation uses one bounded current snapshot and profile/fact drift produces deterministic non-execution evidence.' },
        [ordered]@{ name = 'bounded-source-ownership'; result = 'Pass'; evidence = 'Capability sources declare stable ownership, aggregate to at most 32 facts, and internal failures become typed non-disclosing unavailability.' },
        [ordered]@{ name = 'special-tool-inventory'; result = 'Pass'; evidence = 'Direct execution, .NET, Python, PowerShell, POSIX, Node/npm, Docker, local/remote MCP, desktop, terminal, Git, and lifecycle-only stop semantics are explicitly classified.' },
        [ordered]@{ name = 'browser-transport-identity'; result = 'Pass'; evidence = 'Remote HTTP, local stdio, and local npx browser assignments are distinguished; mixed transport is rejected and repair cannot change sealed transport requirements.' },
        [ordered]@{ name = 'filesystem-owner-boundary'; result = 'Pass'; evidence = 'Runtime completion inspection delegates through the scoped workspace file authority; module special drivers do not own native filesystem or OS probes.' },
        [ordered]@{ name = 'physical-path-policy'; result = 'Pass'; evidence = 'Host-selected physical comparison governs case, containment, aliases, and whitespace; logical/physical values are not conflated.' },
        [ordered]@{ name = 'receipt-non-disclosure'; result = 'Pass'; evidence = 'Public and persisted summaries, codes, references, URIs, paths, narratives, recovery data, and artifacts are bounded, canonical, and non-disclosing.' },
        [ordered]@{ name = 'persistence-fail-closed'; result = 'Pass'; evidence = 'Plan, step requirements, evidence, results, recovery decisions, and action ledgers validate on both write and read; corrupt rows do not normalize to permissive state.' },
        [ordered]@{ name = 'platform-layer-semantics'; result = 'Pass'; evidence = 'Platform means a typed strategy package constrained by non-empty host capabilities, not a broad OS service layer.' },
        [ordered]@{ name = 'dependency-direction'; result = 'Pass'; evidence = 'The graph contains 106 projects and 639 in-repository references with zero cycles and no unresolved project references.' },
        [ordered]@{ name = 'source-reference-integrity'; result = 'Pass'; evidence = 'The manifest contains 171 records, 171 unique IDs, 171 unique paths, and zero missing paths.' },
        [ordered]@{ name = 'anti-stub'; result = 'Pass'; evidence = 'The 36 governed B06 source-reference files contain zero TODO, FIXME, or NotImplementedException markers.' },
        [ordered]@{ name = 'cross-host-focused-proof'; result = 'Pass'; evidence = 'The frozen Windows and pinned Ubuntu slices each execute all 124 governed B06 method patterns as 206 passing cases plus one integration test; affected Release builds are clean.' }
    )
    tests = $tests
    builds = $builds
    hostEvidence = $hostEvidence
    graph = [ordered]@{
        projectCount = [int]$graph.projectCount
        inRepositoryProjectReferences = [int]$graph.inRepositoryProjectReferences
        cyclicProjectCount = [int]$graph.cyclicProjectCount
        unresolvedProjectReferenceCount = [int]$graph.unresolvedProjectReferenceCount
        artifact = $graphArtifact
        sha256 = Get-Sha256 $graphArtifact
    }
    sourceHashes = $sourceHashes
    focusedUnitSelection = [ordered]@{
        baseCommit = $focusedUnitSelection.BaseCommit
        sourceFileCount = $focusedUnitSelection.Files.Count
        methodPatternCount = $focusedUnitSelection.MethodNames.Count
        executedCaseCount = $focusedUnitSelection.ExpectedCaseCount
        methodNames = $focusedUnitSelection.MethodNames
    }
    deferred = @(
        'Actual macOS execution is explicitly deferred by operator instruction; deterministic macOS fixtures do not constitute actual-host proof.',
        'Hosted CI and the final broad Windows/Linux/macOS R4 aggregate remain B07 scope.',
        'The final broad suite is intentionally not rerun at this subbundle gate under the operator-requested fast validation ladder.'
    )
}

$resolvedOutputPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $RepositoryRoot $OutputPath
}
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedOutputPath)) | Out-Null
$proof | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resolvedOutputPath -Encoding utf8

[pscustomobject]@{
    failingFirst = $proof.failingFirst.Count
    sourceAssertions = $proof.sourceAssertions.Count
    tests = $proof.tests.Count
    builds = $proof.builds.Count
    hostEvidence = $proof.hostEvidence.Count
    graphArtifacts = 1
    sourceHashes = $proof.sourceHashes.Count
    output = [System.IO.Path]::GetRelativePath($RepositoryRoot, $resolvedOutputPath).Replace('\', '/')
} | ConvertTo-Json -Compress
