# SB048 Red-Team: Failure Triage Shallow Proof Rejected

## Rejected Claim
"Gate P passes because there are error strings in logs and a run can fail."

## Why That Is Insufficient
- Log text alone does not prove typed failure categories.
- A failed run alone does not prove the operator can see a recommended recovery action.
- API success alone does not prove blocked-step ownership, recovery options, run-health recommendations, invariant diagnostics, and outbox health are projected consistently.
- Browser/UI recovery proof from SB030 does not replace source-backed failure taxonomy and operator troubleshooting tests.

## Required Proof Shape
- Typed taxonomy in source: `AgentFailureCategory`, `AgentRecoveryMode`, `ProcessStepBlockReasonCode`, `ProcessStepRecoveryOption`, and `ProcessRecoveryClassification`.
- Persistence/readback path: blocked-step state writes `BlockReasonCode`, `RecoveryOptionsJson`, and `NextRecoveryAction`, then run detail/read models project those fields.
- Tests: recovery model tests, operator read-model tests, and API run-health serialization tests must pass together.
- Guard scans: no transient bundle paths and no process driver runtime host/registry/selector/manager-command surface.

## Accepted Evidence
- `bundle://proof/SB048/transcripts/failure-triage-observability-tests.txt`
- `bundle://proof/SB048/transcripts/source-assertions.txt`
- `bundle://proof/SB048/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB048/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- `bundle://proof/SB048/transcripts/production-driver-runtime-host-scan.txt`
