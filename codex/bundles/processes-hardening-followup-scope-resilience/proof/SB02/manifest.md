# SB02 Proof Manifest

## Status

- Completed

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs line 799 projects completed subprocess artifacts before finalizer transition.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs line 803 uses ProcessStepCompletionExecutorKind.SubprocessParent.
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs line 958 records projection gaps instead of synthetic artifacts.
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs line 13181 asserts direct, workflow, and subprocess paths use the process-owned finalizer.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Subprocess projection gap diagnostic | ProjectCompletedSubprocessArtifactsAsync; source: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | Process-owned finalizer validation; proof: bundle://proof/SB02/transcripts/source-assertions.txt | Recorded when a completed child run lacks a materializable source artifact; passing command: bundle://proof/SB02/transcripts/passing.txt | DispatchSource_routes_direct_and_workflow_completion_through_process_owned_finalizer asserts source-less placeholders are absent.; negative transcript: bundle://proof/SB02/transcripts/failing-first.txt |

## Failing-First Proof

- Transcript: bundle://proof/SB02/transcripts/failing-first.txt
- Summary: Pre-change source behavior routed completed subprocess parents directly and used sourceArtifact fallback, so required artifacts could be bypassed.

## Passing Proof

- Transcript: bundle://proof/SB02/transcripts/passing.txt
- Tests: DispatchSource_routes_direct_and_workflow_completion_through_process_owned_finalizer; ArtifactContractValidation_accepts_subprocess_artifact_with_current_child_lineage

## Semantic Invariants

- Contract: bundle://proof/SB02/semantic-invariants.md
- Invariant: SB02-INV-001

## Anti-Stub Audit

- Transcript: bundle://proof/SB02/transcripts/anti-stub-audit.txt
- Result: No production stubs, no NotImplementedException placeholders, and no fake artifact satisfiers were introduced.

## Changed-File Hashes

- Transcript: bundle://proof/SB02/transcripts/changed-file-hashes.txt
- 2E4451B605ED202E0100D084993A623EF177FC20E81B79D35623074C97EA7385  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- 5B7219D5142FBE47BD91987F46BEEA07D78DDEC12C81BBFD59C99A642551F0DD  repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- DDE0D4670E819160395A994D3A89A853021544520931DE246582ABB32385FAEE  repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Validation

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests" exited 0 with 409 passed tests.
- dotnet build CanDoItAll.slnx --no-restore exited 0 with existing EF Core assembly-version warnings and zero errors.

## Blockers

- None.

