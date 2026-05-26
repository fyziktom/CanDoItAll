# SB03 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`: manual/API completion now calls `ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts` instead of the old local artifact kind/sensitivity/trust-only check.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`: the shared finalizer-grade validator owns placeholder, format, producer, current-run, and managed-path validation; `ProcessStepCompletionExecutorKind.Manual` preserves manual-transition diagnostics and fingerprints.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`: integration tests prove manual completion rejects placeholder required artifacts and malformed JSON required artifacts through `TransitionStepAsync`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB03 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs | bundle://proof/SB03/manifest.md | bundle://proof/SB03/transcripts/passing.txt | bundle://proof/SB03/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB03/semantic-invariants.md`

## Failing-First or Red-Team Proof

- Transcript: `bundle://proof/SB03/transcripts/failing-first.txt`

## Passing Proof

- Transcript: `bundle://proof/SB03/transcripts/passing.txt`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_SB03_INV_001_rejects_placeholder_required_artifact_on_manual_completion`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_SB03_INV_002_rejects_malformed_json_required_artifact_on_manual_completion`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.TransitionStepAsync_requires_recorded_required_artifacts_before_completion`

## Anti-Stub Audit

- Transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

- Transcript: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`
- `83130A000A72526E72E1FFB896C93C222DDF0C9D7A6FFA6FAD23C19935C0B80C` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`
- `8B0F5DEC62C7B4CB4BBFE980554B2F117232EE3E5054386E08E88286C4008384` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `3DE6048BDE7170CC5D46CB00E0CD2B49CBFDDB58639E7B9AB385EFCA0EC2BE33` `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Validation

- Focused integration tests passed: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~TransitionStepAsync_requires_recorded_required_artifacts_before_completion|FullyQualifiedName~TransitionStepAsync_SB03_INV_001_rejects_placeholder_required_artifact_on_manual_completion|FullyQualifiedName~TransitionStepAsync_SB03_INV_002_rejects_malformed_json_required_artifact_on_manual_completion|FullyQualifiedName~TransitionStepAsync_replaces_pending_decision_record_summary_on_completion|FullyQualifiedName~TransitionStepAsync_accepts_required_artifact_recorded_by_title_without_explicit_expectation_id|FullyQualifiedName~TransitionStepAsync_allows_repair_branch_without_positive_required_artifact"`.

## Blockers

None.


