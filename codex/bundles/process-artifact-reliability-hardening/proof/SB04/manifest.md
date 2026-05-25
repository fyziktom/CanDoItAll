# SB04 Proof Manifest

## Status

Completed.

## Source Assertions

- Placeholder, gap marker, stale/wrong-run, weak producer, and format validation are enforced by `ValidateArtifactCandidate` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`.
- Existing managed files, workspace writes, response text, and provider-native browser projections still project records, but final satisfaction is decided only after process-owned validation.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`.
- Source assertion transcript: `bundle://proof/SB04/transcripts/source-assertions.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| ProcessArtifactValidationStatus.PlaceholderOnly | `ContainsPlaceholderArtifactSignal` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Finalizer blocked/recovery decision | Produced when placeholder, gap, missing-artifact, or unavailable signals are found; proof in `bundle://proof/SB04/transcripts/source-assertions.txt` | `ArtifactContractValidation_rejects_placeholder_record_for_required_artifact` in `bundle://proof/SB04/transcripts/passing.txt` |
| ProcessArtifactValidationStatus.StaleOrWrongRun | `IsCurrentRunArtifact` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Finalizer validation result set | Rejects records not bound to current run, step, execution run, or workflow run; source proof in `bundle://proof/SB04/transcripts/source-assertions.txt` | Placeholder and focused integration transcripts prove weak records do not satisfy required artifacts |

## Failing-First Proof

- Failing-first: N/A for this process hardening extension; SB04 was validated as a negative projection/validation guard after SB01-SB02 introduced the process-owned finalizer.

## Passing Proof

- Transcript path: `bundle://proof/SB04/transcripts/passing.txt`
- Test name: `ArtifactContractValidation_rejects_placeholder_record_for_required_artifact`
- Additional regression proof: `bundle://proof/SB06/transcripts/focused-integration-tests.txt`

## Anti-Stub Audit

- Transcript path: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

- Transcript path: `bundle://proof/SB04/transcripts/changed-file-hashes.txt`
- SHA-256 sample: `976f15d32e4eb1636d2b7d4af44c73278b484cc51c3defd7179e448b7fa3c9e3`

## Blockers

None.
