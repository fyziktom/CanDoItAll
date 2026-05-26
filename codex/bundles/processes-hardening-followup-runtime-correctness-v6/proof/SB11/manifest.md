# SB11 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBlockStateClassifier.cs` owns typed block reason classification, inferred block causes, and recovery option selection.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs` delegates block-state application to `ProcessBlockStateClassifier` before routing through `ProcessRecoveryRouter`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` uses `ProcessBlockStateClassifier` for completion-failure block cause inference.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs` owns step health recovery classification, actionable reason construction, manual-rerun eligibility, and recovery-state exposure.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs` delegates initial step health construction to `ProcessHealthInvariantAuditor`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs` owns workflow output mapping and subprocess child artifact mapping, including legacy fallback warnings and ambiguity blocking.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` routes workflow artifact projection through `WorkflowSubprocessArtifactMapper`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` routes subprocess projection through `WorkflowSubprocessArtifactMapper`.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` directly tests the classifier, health auditor, mapper, and SB09/SB10 regression behavior.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB11 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBlockStateClassifier.cs | bundle://proof/SB11/manifest.md | bundle://proof/SB11/transcripts/passing.txt | bundle://proof/SB11/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB11/semantic-invariants.md`

## Failing-First or Red-Team Proof

Transcript: `bundle://proof/SB11/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB11/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB11/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB11/transcripts/changed-file-hashes.txt`

- `317F232F0A3A769D6C10BE6A5CE0943C123593CED9CE85038F232DDDE7B5F524` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBlockStateClassifier.cs`
## Validation

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~SB11_INV|FullyQualifiedName~ProcessRecoveryRouter_SB10_INV|FullyQualifiedName~WorkflowArtifactProjectionMapping_SB09_INV|FullyQualifiedName~SubprocessArtifactProjectionMapping_SB09_INV"` passed: 15 tests.
- Existing EF Core relational version MSB3277 warnings remain unrelated to SB11.
- SQLite audit found no SB11 SQLite runtime or migration dependency.

## Blockers

None.




