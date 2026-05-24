# SB05 — PostgreSQL claimed-work parallel processing

## Status

Prepared.

## Objective

Turn batch claims into safe bounded parallel processing.

## Covered Inputs

- User requested review of latest `db-remove-sqlite` branch.
- User requested another DB bottleneck review after SQLite removal.
- User requested canonicality protection while unlocking PostgreSQL performance.

## Prerequisites

See dependency map in `plan/01-phase-plan.md`.

## Exact Source References

- `repo://src/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs`
- `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
- `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`
- `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`

## Deliverables

- Add configurable parallelism options for automation deliveries, process outbox, and connector outbox.
- Automation: return `EnvelopeId` from claim SQL or reload before scheduling; process groups with no same-envelope parallelism unless aggregate updates are made race-safe.
- Process outbox: partition by `ProcessRunId` and command class where order matters; allow independent process runs to execute in parallel.
- Connector outbox: define partition key by connector/plugin/account/target if available; keep per-partition max concurrency configurable.
- Use fresh DbContext per item.
- Add concurrency stress tests with duplicate detection and final state assertions.

## Dependency Impact

This subbundle affects downstream canonicality and throughput proof. If it fails, later subbundles must not claim merge readiness.

## Validation Depth

Critical semantic validation is required. Do not rely only on build success.

## Implementation Steps

1. Add configurable parallelism options for automation deliveries, process outbox, and connector outbox.
2. Automation: return `EnvelopeId` from claim SQL or reload before scheduling; process groups with no same-envelope parallelism unless aggregate updates are made race-safe.
3. Process outbox: partition by `ProcessRunId` and command class where order matters; allow independent process runs to execute in parallel.
4. Connector outbox: define partition key by connector/plugin/account/target if available; keep per-partition max concurrency configurable.
5. Use fresh DbContext per item.
6. Add concurrency stress tests with duplicate detection and final state assertions.

## Scope Exceptions

Do not modify `CanDoItAll.IPFS`. Do not reintroduce SQLite support.

## Do Not Do

- Do not hide validation failures.
- Do not weaken canonical runtime DB semantics.
- Do not replace durable DB ownership with only in-memory locks.
- Do not add comments in non-English inside source code.

## Acceptance Checklist

- [ ] Claimed work can execute concurrently with bounded limits.
- [ ] No duplicate processing under multi-worker stress.
- [ ] Aggregate rows remain correct.
- [ ] Default parallelism is conservative but greater than one where safe.

## Proof Required

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- stress test transcripts
- before/after benchmark or deterministic throughput comparison

## Browser Validation Logging

N/A unless UI-visible behavior is changed.

## Progression Gate

All acceptance checklist items must pass, and `proof/SB05/manifest.md` must contain changed-file hashes, command transcripts, source assertions, and residual risk notes.

## Suggested Agent Prompt

Execute SB05 only. Read this README, implement the scoped changes, run the required validation, write the proof manifest, and stop before downstream work unless the progression gate passes.
