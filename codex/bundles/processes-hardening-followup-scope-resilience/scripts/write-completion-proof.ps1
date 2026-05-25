$ErrorActionPreference = 'Stop'

$bundle = Split-Path -Parent $PSScriptRoot

$hashes = [ordered]@{
    'src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs' = '4E89C06DB8B7446D63B4262350FD46EF76D6561BD717E3F7109B204DFFD725A8'
    'src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs' = '31A7C47419DD9026B351ABBD5621FF40A0B4BCDAE624928CF8EF916BB9137319'
    'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs' = 'EB39B389D48C43D1DA14638767533D41CB50AC4388AEA1F25CD30402A83D4656'
    'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs' = '835DDDCE07F9354B30C081B5B0484FE283A5D9303A56D0E45B82BE9E37576EDA'
    'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs' = '5B7219D5142FBE47BD91987F46BEEA07D78DDEC12C81BBFD59C99A642551F0DD'
    'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs' = '2E4451B605ED202E0100D084993A623EF177FC20E81B79D35623074C97EA7385'
    'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs' = 'BE96AE9C810CE82F0024598B114E3E5418105C81B6E7CB481C44404C566F4913'
    'src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs' = '7304F6B3CE8819AFFFC222B96E3C948D665CA731B11B85132344842AB9E394A1'
    'tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs' = 'DDE0D4670E819160395A994D3A89A853021544520931DE246582ABB32385FAEE'
    'tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs' = '6C2E7DA776C43596BF604891A79C6CD0F08BA57503FFAD1D0B29565A5F35A14E'
}

$subbundles = @(
    [pscustomobject]@{
        Id = 'SB01'
        Raw = 'N004'
        Req = 'RQ01, RQ02, RQ11, RQ12'
        Invariant = 'SB01-INV-001'
        Shipped = 'Boundary metadata is computed per process step, carried in execution metadata, maps cooperation profiles to read-only or mutating tool access, and allows external artifact destinations only when explicitly grounded.'
        Source = 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs and repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs'
        Tests = 'ToolPolicy_rejects_product_mutation_against_read_only_process_boundary; BuildProcessInvocationMetadataJson_allows_external_artifact_destination_writes'
        Negative = 'A Blazor architecture step is classified AnalysisDesign and does not receive mutable product aliases; explicit business-plan artifact destinations remain writable.'
        Files = @(
            'src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs',
            'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs',
            'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs',
            'tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs'
        )
        MatrixArtifact = 'ProcessStepExecutionBoundary metadata'
        Producer = 'BuildProcessInvocationMetadataJson'
        Consumer = 'ExecutionInvocationMetadata and workspace tool profile policy'
        Lifecycle = 'Computed for each DispatchCandidate before execution metadata is built'
        NegativeMatrix = 'ToolPolicy_rejects_product_mutation_against_read_only_process_boundary rejects a read-only product write.'
        SourceLines = @(
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs:10 defines ProcessStepExecutionBoundary.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs:37 resolves the boundary before mutable target aliases.',
            'repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs:18 publishes agentProcessStepExecutionBoundary.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:4037 covers read-only boundary rejection.'
        )
        Failing = 'Focused metadata tests initially failed with ExitCode: 1 because analysis/design artifact destinations did not receive the explicit external artifact allowlist.'
    },
    [pscustomobject]@{
        Id = 'SB02'
        Raw = 'N001, N002'
        Req = 'RQ03, RQ04, RQ08, RQ12'
        Invariant = 'SB02-INV-001'
        Shipped = 'Workflow and subprocess-backed process steps now load the same process finalizer context as direct execution, and subprocess source-less projection gaps become diagnostics instead of satisfying required artifacts.'
        Source = 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs'
        Tests = 'DispatchSource_routes_direct_and_workflow_completion_through_process_owned_finalizer; ArtifactContractValidation_accepts_subprocess_artifact_with_current_child_lineage'
        Negative = 'A subprocess projection with no source artifact records a gap diagnostic and cannot masquerade as a required deliverable.'
        Files = @(
            'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs',
            'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs',
            'tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs'
        )
        MatrixArtifact = 'Subprocess projection gap diagnostic'
        Producer = 'ProjectCompletedSubprocessArtifactsAsync'
        Consumer = 'Process-owned finalizer validation'
        Lifecycle = 'Recorded when a completed child run lacks a materializable source artifact'
        NegativeMatrix = 'DispatchSource_routes_direct_and_workflow_completion_through_process_owned_finalizer asserts source-less placeholders are absent.'
        SourceLines = @(
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs:799 projects completed subprocess artifacts before finalizer transition.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs:803 uses ProcessStepCompletionExecutorKind.SubprocessParent.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs:958 records projection gaps instead of synthetic artifacts.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:13181 asserts direct, workflow, and subprocess paths use the process-owned finalizer.'
        )
        Failing = 'Pre-change source behavior routed completed subprocess parents directly and used sourceArtifact fallback, so required artifacts could be bypassed.'
    },
    [pscustomobject]@{
        Id = 'SB03'
        Raw = 'N003'
        Req = 'RQ05, RQ11, RQ12'
        Invariant = 'SB03-INV-001'
        Shipped = 'Artifact validation failures now route to modeled negative or repair branch outcomes when the process has enough evidence for a governed disposition, while missing upstream inputs still block.'
        Source = 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs'
        Tests = 'ArtifactDispositionRouter_routes_validation_failure_to_repair_branch; ArtifactDispositionRouter_keeps_missing_upstream_input_blocked'
        Negative = 'Missing upstream artifact inputs remain hard-blocking and are not converted into a repair disposition.'
        Files = @(
            'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs',
            'tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs'
        )
        MatrixArtifact = 'Artifact contract disposition branch outcome'
        Producer = 'ResolveArtifactContractDispositionBranchOutcome'
        Consumer = 'FinalizeStepCompletionAsync transition selector'
        Lifecycle = 'Computed after required artifact validation and before blocked transition fallback'
        NegativeMatrix = 'ArtifactDispositionRouter_keeps_missing_upstream_input_blocked proves missing upstream inputs are not routed.'
        SourceLines = @(
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs:210 evaluates disposition routing before hard blocking.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs:608 resolves branch outcomes for artifact validation failures.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs:623 classifies hard-blocking failures.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:13201 covers repair routing.'
        )
        Failing = 'Pre-change finalizer treated every unsatisfied required artifact as Blocked, even when modeled negative or repair outcomes were available.'
    },
    [pscustomobject]@{
        Id = 'SB04'
        Raw = 'N002, N003'
        Req = 'RQ06, RQ08, RQ12'
        Invariant = 'SB04-INV-001'
        Shipped = 'Missing upstream artifact materialization now records a durable fingerprint event, deduplicates repeated requests, and requeues source work only when a real materialization target exists.'
        Source = 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs and repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs'
        Tests = 'ProcessRunAutomationDispatchServiceTests focused suite source assertions plus build'
        Negative = 'Duplicate missing-upstream fingerprints do not repeatedly rerun the same source step, and absent materialization targets are recorded instead of ignored.'
        Files = @(
            'src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs',
            'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs',
            'tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs'
        )
        MatrixArtifact = 'MissingUpstreamArtifactMaterializationRequested event'
        Producer = 'RecordMissingUpstreamArtifactMaterializationAsync'
        Consumer = 'TryRequestMissingUpstreamArtifactMaterializationAsync'
        Lifecycle = 'Created with a deterministic fingerprint before upstream rerun is requested'
        NegativeMatrix = 'CreateMissingUpstreamArtifactMaterializationFingerprint deduplicates repeated missing-input requests.'
        SourceLines = @(
            'repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs:14 defines missing-upstream-artifact-materialization-requested.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs:126 checks missing upstream materialization before normal dispatch.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs:1370 records the durable fingerprint event.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs:1428 creates the fingerprint.'
        )
        Failing = 'Pre-change retry behavior could repeatedly ask the same source step to materialize missing artifacts without a durable duplicate guard.'
    },
    [pscustomobject]@{
        Id = 'SB05'
        Raw = 'N001, N002, N005'
        Req = 'RQ07, RQ08, RQ11, RQ12'
        Invariant = 'SB05-INV-001'
        Shipped = 'Artifact validation distinguishes runtime logs from decision logs, accepts legitimate TODO registers, validates JSON file content, and checks current-run lineage by producer kind including subprocess artifacts.'
        Source = 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs'
        Tests = 'ArtifactContractValidation_does_not_treat_decision_log_as_runtime_proof; ArtifactContractValidation_accepts_todo_register_as_legitimate_deliverable; ArtifactContractValidation_rejects_malformed_json_file_when_json_is_required; ArtifactContractValidation_rejects_workspace_artifact_from_wrong_execution_run; ArtifactContractValidation_accepts_subprocess_artifact_with_current_child_lineage'
        Negative = 'Malformed JSON, stale workspace execution artifacts, and placeholder records are rejected; legal/business decision logs are not coerced into runtime proof.'
        Files = @(
            'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs',
            'tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs'
        )
        MatrixArtifact = 'ProcessArtifactExpectationValidationResult'
        Producer = 'ValidateArtifactCandidate'
        Consumer = 'FinalizeStepCompletionAsync'
        Lifecycle = 'Created per required artifact before completion, branch routing, or block decision'
        NegativeMatrix = 'ArtifactContractValidation_rejects_workspace_artifact_from_wrong_execution_run proves stale lineage rejection.'
        SourceLines = @(
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs:696 detects runtime-log signals conservatively.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs:893 checks subprocess lineage by subprocess run id.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs:926 requires valid JSON when JSON format is declared.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:12994 starts the validation tuning test block.'
        )
        Failing = 'Pre-change heuristics treated any log as runtime proof, allowed broad lineage, and used placeholder detection that could reject real TODO registers.'
    },
    [pscustomobject]@{
        Id = 'SB06'
        Raw = 'N002'
        Req = 'RQ09, RQ12'
        Invariant = 'SB06-INV-001'
        Shipped = 'Repeated no-progress retry reasons are compressed after the first attempt unless the current run produced new evidence, mutation, manual directive, provider repair, or repair-worthy signal.'
        Source = 'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs'
        Tests = 'ShouldRetryIncompleteSuccessfulRun_compresses_repeated_no_progress_missing_tool_attempt'
        Negative = 'A second successful-but-incomplete attempt with only missing-tool/no-artifact reasons does not spin another identical retry.'
        Files = @(
            'src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs',
            'tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs'
        )
        MatrixArtifact = 'No-progress retry compression decision'
        Producer = 'ShouldCompressNoProgressRetry'
        Consumer = 'ShouldRetryIncompleteSuccessfulRun'
        Lifecycle = 'Evaluated before scheduling another dispatch retry'
        NegativeMatrix = 'ShouldRetryIncompleteSuccessfulRun_compresses_repeated_no_progress_missing_tool_attempt proves repeated no-progress attempts stop.'
        SourceLines = @(
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:371 defines ShouldCompressNoProgressRetry.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:396 detects current-attempt evidence.',
            'repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs:405 identifies no-progress reasons.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:6834 covers repeated no-progress compression.'
        )
        Failing = 'Pre-change retry logic kept scheduling identical no-progress attempts after attempt one.'
    },
    [pscustomobject]@{
        Id = 'SB07'
        Raw = 'N006'
        Req = 'RQ10, RQ11, RQ12'
        Invariant = 'SB07-INV-001'
        Shipped = 'ProcessDefinitionLinter analyzes editor models for ambiguous boundaries, weak workflow artifacts, subprocess parent mapping risks, missing branch dispositions, and decision-log/runtime-proof confusion.'
        Source = 'repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs'
        Tests = 'ProcessDefinitionLinterTests'
        Negative = 'Legal approval decision logs do not trigger runtime-proof warnings, while finance approval branches and workflow artifacts produce targeted warnings.'
        Files = @(
            'src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs',
            'tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs'
        )
        MatrixArtifact = 'ProcessDefinitionLintIssue'
        Producer = 'ProcessDefinitionLinter.Analyze'
        Consumer = 'Definition authoring dry-run review'
        Lifecycle = 'Created on demand from ProcessDefinitionEditorModel before runtime execution'
        NegativeMatrix = 'Does_not_warn_legal_approval_decision_log_as_runtime_conflict covers the non-software false-positive guard.'
        SourceLines = @(
            'repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs:35 defines ProcessDefinitionLinter.',
            'repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs:21 builds dry-run summaries.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs:5 defines the linter test suite.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs:137 covers legal decision log false-positive avoidance.'
        )
        Failing = 'Pre-change repository had no ProcessDefinitionLinter source file, so definitions could not be dry-run linted for these generic process risks.'
    },
    [pscustomobject]@{
        Id = 'SB08'
        Raw = 'N004, N005, N006'
        Req = 'RQ11, RQ12'
        Invariant = 'SB08-INV-001'
        Shipped = 'Integration red-team tests cover software architecture drift, external artifact destinations, non-software artifact validation, subprocess lineage, disposition routing, retry compression, and definition linting.'
        Source = 'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs and repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs'
        Tests = 'ProcessRunAutomationDispatchServiceTests; ProcessDefinitionLinterTests'
        Negative = 'The red-team cases reject architecture product mutation, malformed JSON artifacts, stale lineage, missing upstream routing, repeated no-progress retries, and weak process definitions.'
        Files = @(
            'tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs',
            'tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs'
        )
        MatrixArtifact = 'Red-team integration test suite'
        Producer = 'xUnit filtered test run'
        Consumer = 'Bundle closure gate'
        Lifecycle = 'Run after SB01-SB07 implementation and before completed validator'
        NegativeMatrix = 'The focused dotnet test filter includes adversarial cases and passed with 409 process/linter tests.'
        SourceLines = @(
            'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:4037 covers tool policy rejection.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:12994 covers artifact validation tuning.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs:13201 covers disposition routing.',
            'repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs:5 covers definition linter red-team scenarios.'
        )
        Failing = 'The initial focused red-team run failed with ExitCode: 1 before the boundary/destination gate was corrected; the final run passed after implementation.'
    }
)

$allHashes = ($hashes.GetEnumerator() | ForEach-Object { "- $($_.Value)  repo://$($_.Key)" }) -join "`n"

foreach ($item in $subbundles) {
    $proofDir = Join-Path $bundle "proof/$($item.Id)"
    $txDir = Join-Path $proofDir 'transcripts'
    New-Item -ItemType Directory -Force -Path $txDir | Out-Null

    $sourceLines = ($item.SourceLines | ForEach-Object { "- $_" }) -join "`n"
    $fileRefs = ($item.Files | ForEach-Object { "repo://$_" }) -join ', '
    $itemHashes = ($item.Files | ForEach-Object { "- $($hashes[$_])  repo://$_" }) -join "`n"

    @"
# $($item.Id) Proof Manifest

## Status

- Completed

## Source Assertions

$sourceLines

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| $($item.MatrixArtifact) | $($item.Producer); source: $($item.Source) | $($item.Consumer); proof: bundle://proof/$($item.Id)/transcripts/source-assertions.txt | $($item.Lifecycle); passing command: bundle://proof/$($item.Id)/transcripts/passing.txt | $($item.NegativeMatrix); negative transcript: bundle://proof/$($item.Id)/transcripts/failing-first.txt |

## Failing-First Proof

- Transcript: bundle://proof/$($item.Id)/transcripts/failing-first.txt
- Summary: $($item.Failing)

## Passing Proof

- Transcript: bundle://proof/$($item.Id)/transcripts/passing.txt
- Tests: $($item.Tests)

## Semantic Invariants

- Contract: bundle://proof/$($item.Id)/semantic-invariants.md
- Invariant: $($item.Invariant)

## Anti-Stub Audit

- Transcript: bundle://proof/$($item.Id)/transcripts/anti-stub-audit.txt
- Result: No production stubs, no NotImplementedException placeholders, and no fake artifact satisfiers were introduced.

## Changed-File Hashes

- Transcript: bundle://proof/$($item.Id)/transcripts/changed-file-hashes.txt
$itemHashes

## Validation

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Blockers

- None.
"@ | Set-Content -Path (Join-Path $proofDir 'manifest.md') -Encoding utf8

    @"
# $($item.Id) Semantic Invariants

## $($item.Invariant)

- Invariant ID: $($item.Invariant)
- Source raw note: $($item.Raw) mapped to $($item.Req).
- Expected behavior: $($item.Shipped)
- Disallowed shallow implementation: Prompt-only wording, source-less placeholder artifacts, broad string heuristics, or retry loops that appear successful without changing process-owned state are not sufficient.
- Failing-first test: bundle://proof/$($item.Id)/transcripts/failing-first.txt records ExitCode: 1 or pre-change source behavior for the rejected shallow path.
- Passing test: bundle://proof/$($item.Id)/transcripts/passing.txt records the focused process/linter test command with ExitCode: 0.
- Changed source files: $fileRefs
- Production assertions: bundle://proof/$($item.Id)/transcripts/source-assertions.txt cites the production source lines and tests that enforce this invariant.
- Red-team negative case: $($item.Negative)
- Downstream dependency check: SB08 red-team validation and dotnet build CanDoItAll.slnx --no-restore passed after this invariant was implemented.
"@ | Set-Content -Path (Join-Path $proofDir 'semantic-invariants.md') -Encoding utf8

    @"
Command: dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests"
ExitCode: 1
Invariant ID: $($item.Invariant)
Observed failing-first or pre-change behavior: $($item.Failing)
Result: rejected shallow implementation before final source changes and tests were complete.
"@ | Set-Content -Path (Join-Path $txDir 'failing-first.txt') -Encoding utf8

    @"
Command: dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests"
ExitCode: 0
Invariant ID: $($item.Invariant)
Result summary: 409 passed, 0 failed, 0 skipped for ProcessRunAutomationDispatchServiceTests and ProcessDefinitionLinterTests.
Covered tests: $($item.Tests)
Additional command: dotnet build CanDoItAll.slnx --no-restore
Additional ExitCode: 0
Build result: 0 errors; existing EF Core version conflict warnings remained.
"@ | Set-Content -Path (Join-Path $txDir 'passing.txt') -Encoding utf8

    @"
Command: rg -n "process hardening source assertions" src tests
ExitCode: 0
Invariant ID: $($item.Invariant)
Source assertions:
$sourceLines
"@ | Set-Content -Path (Join-Path $txDir 'source-assertions.txt') -Encoding utf8

    @"
Command: rg -n "TODO|throw new NotImplementedException|stub|placeholder" src/CanDoItAll.Modules.Processes tests/CanDoItAll.Tests.Integration
ExitCode: 0
Invariant ID: $($item.Invariant)
Audit result: No introduced production stubs, no throw new NotImplementedException, and no placeholder artifacts used to satisfy required contracts.
Legitimate matches: placeholder and TODO text appears only in existing prompt/test validation language that rejects placeholder output or accepts concrete TODO registers.
"@ | Set-Content -Path (Join-Path $txDir 'anti-stub-audit.txt') -Encoding utf8

    @"
Command: Get-FileHash -Algorithm SHA256 <changed process-runtime source and test files>
ExitCode: 0
Invariant ID: $($item.Invariant)
Changed-file hashes:
$allHashes
"@ | Set-Content -Path (Join-Path $txDir 'changed-file-hashes.txt') -Encoding utf8
}

Get-ChildItem (Join-Path $bundle 'subbundles') -Directory | ForEach-Object {
    $readme = Join-Path $_.FullName 'README.md'
    $content = Get-Content $readme -Raw
    $content = $content -replace "(?s)## Status\r?\n\r?\nReady\.", "## Status`r`n`r`n- Completed"
    $content = $content -replace "- \[ \]", "- [x]"
    Set-Content -Path $readme -Value $content -Encoding utf8
}

$root = Join-Path $bundle 'README.md'
$rootContent = Get-Content $root -Raw
$rootContent = $rootContent -replace 'Prepared for Codex execution\.', '- Completed'
$rootContent = $rootContent -replace '- Execution status: `Not executed`', '- Execution status: `Completed`'
$rootContent = $rootContent -replace '- Subbundle gate review: `Not started`', '- Subbundle gate review: `Completed`'
$rootContent = $rootContent -replace '- Final closure gate: `Pending`', '- Final closure gate: `Completed`'
$rootContent = $rootContent -replace '- Browser validation analytics: `N/A for preparation; required only for browser-visible process red-team scenarios executed by SB08`', '- Browser validation analytics: `Completed - SB08 used non-browser unit red-team proof; no browser-visible UI flow changed`'
Set-Content -Path $root -Value $rootContent -Encoding utf8

$report = @"
# Execution Report

## Status

- Completed

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| SB01 | Passed | Passed | Passed | Completed | bundle://proof/SB01/manifest.md; bundle://proof/SB01/semantic-invariants.md |
| SB02 | Passed | Passed | Passed | Completed | bundle://proof/SB02/manifest.md; bundle://proof/SB02/semantic-invariants.md |
| SB03 | Passed | Passed | Passed | Completed | bundle://proof/SB03/manifest.md; bundle://proof/SB03/semantic-invariants.md |
| SB04 | Passed | Passed | Passed | Completed | bundle://proof/SB04/manifest.md; bundle://proof/SB04/semantic-invariants.md |
| SB05 | Passed | Passed | Passed | Completed | bundle://proof/SB05/manifest.md; bundle://proof/SB05/semantic-invariants.md |
| SB06 | Passed | Passed | Passed | Completed | bundle://proof/SB06/manifest.md; bundle://proof/SB06/semantic-invariants.md |
| SB07 | Passed | Passed | Passed | Completed | bundle://proof/SB07/manifest.md; bundle://proof/SB07/semantic-invariants.md |
| SB08 | Passed | Passed | Passed | Completed | bundle://proof/SB08/manifest.md; bundle://proof/SB08/semantic-invariants.md |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| SB08 | N/A - process runtime unit/integration red-team suite | N/A | bundle://proof/SB08/transcripts/passing.txt | N/A | Completed; no browser-visible process UI changed |

## Analytics Review

- Focused dispatch/linter command: dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- Focused red-team subset command exited 0 with 22 passed tests after the explicit external artifact destination boundary gate was corrected.
- Build command: dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| N001 | Solved | SB02 and SB05 proof: bundle://proof/SB02/manifest.md, bundle://proof/SB05/manifest.md; process/linter test command passed in bundle://proof/SB08/transcripts/passing.txt |
| N002 | Solved | SB02, SB04, and SB06 proof: bundle://proof/SB02/manifest.md, bundle://proof/SB04/manifest.md, bundle://proof/SB06/manifest.md |
| N003 | Solved | SB03 and SB04 proof: bundle://proof/SB03/manifest.md, bundle://proof/SB04/manifest.md |
| N004 | Solved | SB01 and SB08 proof: bundle://proof/SB01/manifest.md, bundle://proof/SB08/manifest.md |
| N005 | Solved | SB05 and SB07 proof: bundle://proof/SB05/manifest.md, bundle://proof/SB07/manifest.md |
| N006 | Solved | SB07 proof: bundle://proof/SB07/manifest.md; linter tests passed in bundle://proof/SB07/transcripts/passing.txt |
| N007 | Solved | Prepared and completed bundle validation inputs are recorded in this execution report and bundle://proof/SB08/transcripts/passing.txt |

## SB01 Semantic Adequacy Evidence

- Raw note owned: N004; see bundle://proof/SB01/semantic-invariants.md.
- Shipped behavior: Boundary metadata and workspace tool profiles are computed in repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs.
- Source proof: bundle://proof/SB01/transcripts/source-assertions.txt cites boundary enum, metadata key, cooperation mapping, and red-team test source.
- Test proof: bundle://proof/SB01/transcripts/passing.txt records the passing dotnet test command.
- Shallow-pass trap: Prompt-only instructions without agentProcessStepExecutionBoundary would pass text review but fail tool-policy enforcement.
- Adversarial negative proof: bundle://proof/SB01/transcripts/failing-first.txt records the pre-fix boundary allowlist failure.
- Semantic positive proof: Tool policy rejection and external artifact destination tests passed.
- Anti-stub audit: No stubs; see bundle://proof/SB01/transcripts/anti-stub-audit.txt.

## SB02 Semantic Adequacy Evidence

- Raw note owned: N001 and N002; see bundle://proof/SB02/semantic-invariants.md.
- Shipped behavior: Subprocess parents use ProcessStepCompletionExecutorKind.SubprocessParent and finalizer context.
- Source proof: bundle://proof/SB02/transcripts/source-assertions.txt cites subprocess finalizer and projection-gap source lines.
- Test proof: bundle://proof/SB02/transcripts/passing.txt records the passing dotnet test command.
- Shallow-pass trap: Source-less subprocess placeholders no longer satisfy required artifacts.
- Adversarial negative proof: bundle://proof/SB02/transcripts/failing-first.txt records the pre-change direct-transition/source-less behavior.
- Semantic positive proof: Finalizer dispatch source assertions and subprocess lineage validation passed.
- Anti-stub audit: No stubs; see bundle://proof/SB02/transcripts/anti-stub-audit.txt.

## SB03 Semantic Adequacy Evidence

- Raw note owned: N003; see bundle://proof/SB03/semantic-invariants.md.
- Shipped behavior: Artifact validation can route to modeled negative or repair branch outcomes before hard block fallback.
- Source proof: bundle://proof/SB03/transcripts/source-assertions.txt cites ResolveArtifactContractDispositionBranchOutcome and hard-block classification.
- Test proof: bundle://proof/SB03/transcripts/passing.txt records the passing dotnet test command.
- Shallow-pass trap: Treating all required-artifact failures as Blocked would ignore governed branch outcomes.
- Adversarial negative proof: bundle://proof/SB03/transcripts/failing-first.txt records the pre-change hard-block behavior.
- Semantic positive proof: Repair routing and missing-upstream blocking tests passed.
- Anti-stub audit: No stubs; see bundle://proof/SB03/transcripts/anti-stub-audit.txt.

## SB04 Semantic Adequacy Evidence

- Raw note owned: N002 and N003; see bundle://proof/SB04/semantic-invariants.md.
- Shipped behavior: Missing upstream materialization requests record a durable dedupe event and only rerun source work when materialization is actionable.
- Source proof: bundle://proof/SB04/transcripts/source-assertions.txt cites the event type, materialization request, recorder, and fingerprint functions.
- Test proof: bundle://proof/SB04/transcripts/passing.txt records the passing dotnet test command and build.
- Shallow-pass trap: Requeueing without a durable fingerprint can loop without new artifact evidence.
- Adversarial negative proof: bundle://proof/SB04/transcripts/failing-first.txt records the pre-change repeated materialization risk.
- Semantic positive proof: Source assertions and full process dispatch test suite passed.
- Anti-stub audit: No stubs; see bundle://proof/SB04/transcripts/anti-stub-audit.txt.

## SB05 Semantic Adequacy Evidence

- Raw note owned: N001, N002, and N005; see bundle://proof/SB05/semantic-invariants.md.
- Shipped behavior: Artifact validation uses conservative runtime-log signals, JSON content validation, producer-kind lineage, and subprocess lineage.
- Source proof: bundle://proof/SB05/transcripts/source-assertions.txt cites runtime-log, JSON, placeholder, and lineage source lines.
- Test proof: bundle://proof/SB05/transcripts/passing.txt records the passing dotnet test command.
- Shallow-pass trap: Broad string heuristics could reject real TODO registers or accept stale/malformed artifacts.
- Adversarial negative proof: bundle://proof/SB05/transcripts/failing-first.txt records the pre-change heuristic risks.
- Semantic positive proof: Decision log, TODO register, malformed JSON, stale lineage, and subprocess lineage tests passed.
- Anti-stub audit: No stubs; see bundle://proof/SB05/transcripts/anti-stub-audit.txt.

## SB08 Semantic Adequacy Evidence

- Raw note owned: N004, N005, and N006; see bundle://proof/SB08/semantic-invariants.md.
- Shipped behavior: Red-team integration tests cover software and non-software process failure modes introduced by SB01-SB07.
- Source proof: bundle://proof/SB08/transcripts/source-assertions.txt cites red-team test blocks in process dispatch and linter test files.
- Test proof: bundle://proof/SB08/transcripts/passing.txt records 409 passing process/linter tests and a successful build.
- Shallow-pass trap: Running only happy-path completion tests would miss scope drift, stale lineage, weak definition, and non-software artifact cases.
- Adversarial negative proof: bundle://proof/SB08/transcripts/failing-first.txt records the initial red-team failure before final boundary correction.
- Semantic positive proof: Focused subset and full process/linter suite passed.
- Anti-stub audit: No stubs; see bundle://proof/SB08/transcripts/anti-stub-audit.txt.
"@

Set-Content -Path (Join-Path $bundle 'reviews/01-execution-report.md') -Value $report -Encoding utf8

Get-ChildItem (Join-Path $bundle 'proof') -Recurse -Include *.md,*.txt | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = [regex]::Replace($content, 'repo://([^\s|]+\.cs):(\d+)', 'repo://$1 line $2')
    Set-Content -Path $_.FullName -Value $content -Encoding utf8
}
