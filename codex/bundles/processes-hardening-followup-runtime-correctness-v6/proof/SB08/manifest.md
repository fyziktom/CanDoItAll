# SB08 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` defines `IProcessArtifactContentReader`, the workspace fallback reader, and `StorageBackedProcessArtifactContentReader`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` keeps automated finalizer validation on `StorageBackedProcessArtifactContentReader`.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` creates the same storage-backed reader for manual/API completion validation.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` passes `managedArtifactContentReader` into the shared completion artifact validator.
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs` injects storage catalog and driver dependencies required for manual/API completion validation.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` covers manual completion rejection for malformed storage-backed JSON content.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` covers direct storage-backed reader validation and workspace fallback validation.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB08 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs | bundle://proof/SB08/manifest.md | bundle://proof/SB08/transcripts/passing.txt | bundle://proof/SB08/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB08/semantic-invariants.md`

## Failing-First or Red-Team Proof

Transcript: `bundle://proof/SB08/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB08/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

- `24FF58117AB0C69E43023D89004C9F4084407E9F945E7B42081302329B8E9CAE` `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
## Validation

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~TransitionStepAsync_SB08_INV_001|FullyQualifiedName~ArtifactContractValidation_SB04_INV_001_reads_catalog_backed_storage_reference|FullyQualifiedName~ArtifactContractValidation_SB05_INV_001_rejects_malformed_json_from_relative_managed_storage_path|FullyQualifiedName~ArtifactContractValidation_SB05_INV_001_reports_missing_relative_managed_storage_content"` passed: 4 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~TransitionStepAsync_requires_recorded_required_artifacts_before_completion|FullyQualifiedName~TransitionStepAsync_SB03_INV_001_rejects_placeholder_required_artifact_on_manual_completion|FullyQualifiedName~TransitionStepAsync_SB03_INV_002_rejects_malformed_json_required_artifact_on_manual_completion|FullyQualifiedName~TransitionStepAsync_SB08_INV_001_rejects_malformed_storage_backed_json_required_artifact_on_manual_completion|FullyQualifiedName~TransitionStepAsync_replaces_pending_decision_record_summary_on_completion|FullyQualifiedName~TransitionStepAsync_accepts_required_artifact_recorded_by_title_without_explicit_expectation_id|FullyQualifiedName~ArtifactContractValidation_SB04_INV_001_reads_catalog_backed_storage_reference|FullyQualifiedName~ArtifactContractValidation_SB05_INV_001_rejects_malformed_json_from_relative_managed_storage_path|FullyQualifiedName~ArtifactContractValidation_SB05_INV_001_reports_missing_relative_managed_storage_content"` passed: 9 tests.
- Existing EF Core relational version MSB3277 warnings remain unrelated to SB08.
- SQLite audit found only existing retired/legacy strings and bundle prohibition text; no SB08 runtime or migration dependency was added.

## Blockers

None.




