# SB05 Semantic Invariants

## Status

Completed.

## Invariants

- Invariant ID: `SB05-INV-001`
- Source raw note: N001, N006, and N007 require missing or malformed required artifacts to recover or block deterministically instead of repeating the same executor attempt.
- Expected behavior: Required artifact validation failures produce stable fingerprints, diagnostics, optional manager recovery once, and blocked completion when still unsatisfied.
- Disallowed shallow implementation: Lowering retry count globally or returning Completed with missing artifacts.
- Failing-first test: N/A process hardening extension; the missing-artifact negative case is captured in `bundle://proof/SB05/transcripts/passing.txt`.
- Passing test: `ArtifactContractValidation_reports_missing_required_artifact_for_current_step` in `bundle://proof/SB05/transcripts/passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `CreateArtifactFailureFingerprint`, `ArtifactValidationDiagnostic`, `ProcessStepRunStatus.Blocked`, and manager-recovery finalizer routing are asserted by `bundle://proof/SB05/transcripts/source-assertions.txt`.
- Red-team negative case: Missing required artifact returns `Missing` and `IsSatisfied == false`.
- Downstream dependency check: SB06 focused integration and solution build prove the process dispatch regression suite remains green.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| Artifact failure fingerprint | `CreateArtifactFailureFingerprint` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Diagnostic persistence and completion blocking | Recomputed for each required artifact failure and deduped by journal correlation id; proof in `bundle://proof/SB05/transcripts/source-assertions.txt` | Missing-artifact test proves no false satisfaction |
| Blocked artifact contract reason | `BuildArtifactContractBlockedReason` in the same source file | Step transition request through finalizer result | Names exact missing/invalid artifacts before blocked transition; source proof in `bundle://proof/SB05/transcripts/source-assertions.txt` | Focused integration transcript validates dispatch suite after hardening |

## Red-Team Negative Cases

- Repeated missing artifact failures cannot complete as success.
- Stranded manager-recovery completion must pass finalizer validation before transition.
