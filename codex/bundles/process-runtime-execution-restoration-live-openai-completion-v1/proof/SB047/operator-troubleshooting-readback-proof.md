# SB047 Operator Troubleshooting Readback Proof

## Status
Completed.

## Objective
Prove operators can read back actionable troubleshooting state for failed or blocked process runs.

## Source-Backed Behavior
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs` exposes `ProcessStepRunHealthViewModel`, `ProcessRunHealthSummaryViewModel`, `ProcessRuntimeInvariantDiagnosticViewModel`, step `BlockReasonCode`, `NextRecoveryAction`, and `RecoveryOptions`.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRecoveryRouter.cs` selects actionable next recovery actions and classifications such as missing artifact, context reset retry, provider repair retry, automatic retry, and manual rerun.
- API run-detail readback serializes typed block reason, recovery options, next recovery action, missing artifact count, and run-health recommended action.
- Operator read-model tests cover dead-lettered automation health, escalation projection, attempt timeline, invariant diagnostics, manual transition validation failures, and recovery directive hygiene.

## Test Proof
- `bundle://proof/SB048/transcripts/failure-triage-observability-tests.txt` passed with 38 integration tests.
- API tests prove `ArtifactContractUnsatisfied` maps to `RecoverArtifactsOnly` and missing upstream artifacts map to `WaitForArtifactMaterialization`.
- Operator read-model tests prove dead-lettered outbox health, runtime invariant diagnostic recommended actions, recovery/escalation readback, and journaled operator actions.

## Guard Proof
- Source assertions: `bundle://proof/SB048/transcripts/source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB048/transcripts/no-transient-bundle-path-scan.txt`
- Runtime-host drift scan: `bundle://proof/SB048/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Production driver runtime-host scan: `bundle://proof/SB048/transcripts/production-driver-runtime-host-scan.txt`

## Result
No production code changes were required. Operator-facing read models and API details already expose typed, actionable troubleshooting state backed by integration tests.
