# SB02 Semantic Invariants

## Status

Completed.

## Invariants

- Invariant ID: `SB02-INV-001`
- Source raw note: N001, N006, and N007 require valid current evidence, not merely the presence of an artifact record.
- Expected behavior: A required process artifact is satisfied only when its mode, producer type, current-run identity, storage path, and declared format pass validation.
- Disallowed shallow implementation: Marking an expectation complete when any `ProcessArtifactRecord` has the expected id, regardless of producer or evidence quality.
- Failing-first test: Pre-change source assertion in `bundle://proof/SB02/transcripts/failing-first.txt` shows no process-owned validator existed.
- Passing test: `ArtifactContractValidation_rejects_response_text_as_runtime_evidence`, `ArtifactContractValidation_rejects_placeholder_record_for_required_artifact`, `ArtifactContractValidation_reports_missing_required_artifact_for_current_step`, and `ArtifactContractValidation_accepts_matching_workflow_artifact_for_process_expectation` in `bundle://proof/SB02/transcripts/passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `ProcessArtifactValidationStatus.WrongProducerMode`, `ProcessArtifactValidationStatus.PlaceholderOnly`, `ProcessArtifactValidationStatus.Missing`, and `ProcessRuntimeEventTypes.ArtifactValidationDiagnostic` are asserted by `bundle://proof/SB02/transcripts/source-assertions.txt`.
- Red-team negative case: Assistant response text cannot satisfy runtime proof/evidence, and placeholder/gap records cannot satisfy required deliverables.
- Downstream dependency check: SB03 recovery consumes unsatisfied validation results, and SB05 blocking uses the same failure fingerprints instead of retrying blindly.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| ProcessArtifactExpectationValidationResult | `ValidateArtifactExpectationForRecordedArtifacts` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Finalizer recovery/blocking logic in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Recomputed after projection and after recovery; source proof is `bundle://proof/SB02/transcripts/source-assertions.txt` | Passing tests reject response text, placeholders, and missing artifacts in `bundle://proof/SB02/transcripts/passing.txt` |
| ArtifactValidationDiagnostic | `PersistArtifactValidationDiagnosticsAsync` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Process journal persistence through `ProcessJournalEntry` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Created once per failure fingerprint with executor, execution, and workflow provenance; lifecycle proof is `bundle://proof/SB02/transcripts/source-assertions.txt` | Missing and wrong-producer tests prove diagnostics are failure input in `bundle://proof/SB02/transcripts/passing.txt` |
| ProcessArtifactValidationStatus | Candidate validation branches in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Recovery and blocked-reason builder in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Drives completion only through `Satisfied`; verified by `bundle://proof/SB02/transcripts/passing.txt` | Red-team tests assert `WrongProducerMode`, `PlaceholderOnly`, and `Missing` in `bundle://proof/SB02/transcripts/passing.txt` |

## Red-Team Negative Cases

- Final assistant response projected as text is rejected as runtime proof.
- Placeholder/gap marker records are rejected even when they carry the expected artifact id.
- Missing required artifacts produce `Missing`, not satisfied state.
