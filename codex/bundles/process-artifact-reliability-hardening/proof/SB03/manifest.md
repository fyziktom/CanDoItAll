# SB03 Proof Manifest

## Status

Completed.

## Source Assertions

- Manager recovery selection no longer treats a generic `lead` token as eligible recovery authority in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`.
- Explicit recovery capability is recognized by `ContainsExplicitArtifactRecoveryCapability`.
- Recovered outputs return to the process-owned finalizer and are validated again before a completed transition.
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`.
- Source assertion transcript: `bundle://proof/SB03/transcripts/source-assertions.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| ManagerArtifactRecoveryAgent | `ResolveManagerArtifactRecoveryAgent` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs` | `RecoverMissingCompletionArtifactsWithManagerAsync` in the same source file | Created only for assigned manager or explicit artifact recovery capability; proof in `bundle://proof/SB03/transcripts/source-assertions.txt` | `ResolveManagerArtifactRecoveryAgent_rejects_single_generic_lead_fallback_agent` rejects generic lead fallback |
| ProcessStepCompletionExecutorKind.ManagerArtifactRecovery | Stranded recovery call site in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `FinalizeStepCompletionAsync` validation/revalidation path | Recovery completion is routed through finalizer before transition; verified by `bundle://proof/SB01/transcripts/passing.txt` and `bundle://proof/SB03/transcripts/source-assertions.txt` | Failing-first transcript shows pre-change resolver accepted generic lead token |

## Failing-First Proof

- Transcript path: `bundle://proof/SB03/transcripts/failing-first.txt`
- Test name: `ResolveManagerArtifactRecoveryAgent_rejects_single_generic_lead_fallback_agent`
- Test name: `ResolveManagerArtifactRecoveryAgent_allows_single_explicit_artifact_recovery_manager`
- Result: pre-change source assertion exits non-zero because generic `lead` was part of manager-like fallback scoring.

## Passing Proof

- Transcript path: `bundle://proof/SB03/transcripts/passing.txt`
- Result: manager resolver tests reject a single generic lead fallback and accept a single explicitly capable artifact recovery manager.

## Anti-Stub Audit

- Transcript path: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Result: no stub, TODO, or `NotImplementedException` markers exist in changed production source.

## Changed-File Hashes

- Transcript path: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`
- SHA-256 sample: `f14e5c4931842f7a66558dc3d0b72fbe6e605121334de8b08b247fd80c5c120a` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`

## Validation

- `bundle://proof/SB03/transcripts/passing.txt`
- `bundle://proof/SB06/transcripts/focused-integration-tests.txt`
- `bundle://proof/SB06/transcripts/solution-build.txt`

## Blockers

None.
