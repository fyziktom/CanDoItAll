# SB04 Semantic Invariants

## Status

Completed.

## Invariants

- Invariant ID: `SB04-INV-001`
- Source raw note: N001 and N006 require projection records to be distinguished from validated required artifacts.
- Expected behavior: Placeholder, gap, stale, wrong-run, and weak projection records cannot satisfy required artifact expectations.
- Disallowed shallow implementation: Creating a `ProcessArtifactRecord` with the required expectation id and immediately treating that id as satisfied.
- Failing-first test: N/A process hardening extension; the negative guard is captured by `bundle://proof/SB04/transcripts/passing.txt`.
- Passing test: `ArtifactContractValidation_rejects_placeholder_record_for_required_artifact` in `bundle://proof/SB04/transcripts/passing.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `PlaceholderOnly`, `StaleOrWrongRun`, and current-run validation are asserted by `bundle://proof/SB04/transcripts/source-assertions.txt`.
- Red-team negative case: Placeholder records with the expected artifact id are rejected.
- Downstream dependency check: SB05 blocking consumes validation failures instead of projection-local satisfaction flags.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| ProcessArtifactValidationStatus.PlaceholderOnly | `ContainsPlaceholderArtifactSignal` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Finalizer recovery/blocking path | Created from projection records that contain placeholder/gap signals; proof in `bundle://proof/SB04/transcripts/source-assertions.txt` | Placeholder test rejects satisfaction |
| ProcessArtifactValidationStatus.StaleOrWrongRun | `IsCurrentRunArtifact` in the same source file | Finalizer recovery/blocking path | Created when run, step, execution, or workflow provenance does not match; source proof in `bundle://proof/SB04/transcripts/source-assertions.txt` | Focused integration transcript verifies validation remains active |

## Red-Team Negative Cases

- Placeholder/gap marker cannot satisfy a required artifact.
- Wrong-run/stale provenance is rejected by source-level validation.
