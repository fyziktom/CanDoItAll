# Recovery And Provenance Architecture

## Recovery Is Not A Retry

Manager artifact recovery should not rerun broad implementation. It should only recover missing/invalid artifacts from existing run history and evidence. If evidence is insufficient, it must block with a precise gap.

## Recovery Manager Eligibility

Valid recovery manager sources:

1. Explicit `ProcessRun.ManagerAgentId` or equivalent bound process manager technical agent.
2. Explicit process manager assignment with direct messaging/recovery capability.
3. Agent capability/tag such as `process-artifact-recovery-manager` or `artifact-recovery-manager`.

Disallowed fallback:

- generic `lead`
- generic `manager` without process/recovery capability when no process manager is bound
- arbitrary highest-scoring agent based on name alone

## Recovery Provenance Fields

Recovered artifacts or their linked provenance records should expose:

```text
CreatedByKind = Agent | Workflow | ManagerRecovery | SubprocessProjection | Manual | SystemDiagnostic
SourceExecutionRunId?
SourceWorkflowRunId?
RecoveredForExecutionRunId?
RecoveryDecisionId?
ReworkPacketId?
SourceArtifactRecordIds[]
SourceToolReceiptIds[]
ProjectionDiagnosticIds[]
ValidationStatus
ValidationFingerprint
EvidenceCompleteness
SatisfiesExpectation
```

If adding all fields to the artifact record is too large, add a linked provenance table/record and keep `ProcessArtifactRecord` stable.

## Placeholder/Gaps

A missing evidence gap is not a satisfying artifact.

Use separate diagnostics/gap records:

- `MissingArtifactDiagnostic`
- `ArtifactProjectionDiagnostic`
- `SubprocessArtifactGap`
- `RecoveryBlockedDiagnostic`

Do not assign the required `ArtifactExpectationId` as satisfied unless validation passes.
