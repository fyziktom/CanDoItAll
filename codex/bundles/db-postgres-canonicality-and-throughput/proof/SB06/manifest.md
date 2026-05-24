# SB06 proof manifest

## Status

Completed.

## Owned requirements

Make durable process step dispatch claims the canonical mutation precondition, including renewal, artifact projection, completion, branch transition, and failure paths.

## Semantic invariant contract

`bundle://proof/SB06/semantic-invariants.md`

## Changed files

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- Hash proof: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Command transcripts

- `bundle://proof/SB06/transcripts/dispatch-claim-source-audit.txt`
- `bundle://proof/SB08/transcripts/focused-integration-tests.txt`
- `bundle://proof/SB08/transcripts/full-solution-build-final-clean.txt`

## Source assertions

- `RenewStepDispatchClaimAsync` returns `bool`; failure raises `ProcessDispatchClaimLostException` and stops mutation.
- `EnsureStepDispatchClaimHeldAsync` verifies matching unexpired claim token before artifact projection and transitions.
- Transition helpers require a dispatch claim and perform the claim check immediately before mutation.
- Local `StepDispatchGuards` are cleaned after claim release and remain only a local fast path.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Process step dispatch claim | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | `bundle://proof/SB08/transcripts/focused-integration-tests.txt` | `bundle://proof/SB06/transcripts/dispatch-claim-source-audit.txt` |

## Semantic positive proof

Focused integration and build proof pass with claim-aware transitions across dispatch, artifact projection, completion recovery, and failure handling.

## Adversarial negative proof

The source audit shows every claim-sensitive mutation path calls `EnsureStepDispatchClaimHeldAsync` or `TransitionStepWithClaimAsync`; expired or stolen claims cannot renew and cannot commit.

## Residual risks

No pre-change stale-claim failing transcript was captured before production edits. The final closure uses post-change source assertions and focused integration coverage.
