# Dispatch Route And Claim Boundary

## Route decisions

A route decision may be one of:

- `NoCandidate`
- `ClaimUnavailable`
- `FreshRecoverySkip`
- `BlockDatabaseRequirement`
- `MaterializeMissingUpstreamArtifact`
- `StrandedManagerArtifactRecovery`
- `Subprocess`
- `StartInProgress`
- `Workflow`
- `AgentExecution`
- `SkipClosedRun`
- `FinalizeDirectAgent`
- `FinalizeManagerRecovery`
- `ClaimLost`
- `UnhandledFailure`

The planner must not execute those effects. It only classifies facts.

## Claim session

The claim/heartbeat helper may manage local lifecycle around:

- in-memory `SemaphoreSlim`,
- durable `ProcessStepDispatchClaim`,
- heartbeat lifecycle,
- lease renew callback,
- claim lost exception construction/propagation.

It must not decide business route semantics.
