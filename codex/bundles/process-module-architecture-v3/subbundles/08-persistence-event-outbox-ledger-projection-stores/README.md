# SB08 Persistence, Event Store, Outbox, Artifact Ledger Stores, And Projection Storage

## Status

- Completed

Completed on 2026-06-15.

## Objective

Implement EF/PostgreSQL persistence behind runtime ports: runtime state store, event store, outbox, artifact ledger store, projection storage, projector offsets, dead letters, idempotency indexes, schema version/upcaster support, replay, and concurrency behavior.

## Why This Bundle Exists

Runtime must stay persistence-implementation-neutral, but durable reliability requires concrete persistence. This bundle prevents query-built observation from becoming runtime truth.

## Covered Inputs

- REQ-026 through REQ-030.
- v3 runtime persistence/event/outbox architecture.

## Context Reset: Read These First

- SB07 execution report.
- `architecture/12-runtime-persistence-event-store-and-outbox.md`
- `architecture/08-monitoring-events-snapshots-and-ui-projections.md`
- `architecture/07-artifact-error-recovery-and-subprocess-model.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/12-runtime-persistence-event-store-and-outbox.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/08-monitoring-events-snapshots-and-ui-projections.md`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs`

## Source Evidence To Use

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationCache.cs`
- SB01 persistence/archive inventory.

## Prerequisites

- SB07 complete.
- Runtime ports stable.

## In Scope

- EF entities/mappings for runtime state.
- Event store.
- Transactional outbox.
- Artifact ledger store.
- Projection storage.
- Projector offsets.
- Projection dead letters.
- Idempotency unique constraints.
- Event sequence model.
- Upcaster support.
- Replay support.
- Persistence tests.

## Out Of Scope

- No UI components.
- No manager behavior.
- No concrete driver behavior.
- No legacy runtime history compatibility; that is SB12.

## Target Projects / Files

- `src/CanDoItAll.Processes.Persistence`
- persistence tests.

## Deliverables

- Persistence implementation for runtime ports.
- Event/outbox/artifact ledger/projection stores.
- Concurrency and replay tests.

## Expected Deliverables

- Runtime state, events, artifact ledger, and outbox commit atomically.
- Projectors can use offsets and dead letters.
- Projections are derived and rebuildable.

## Dependency Impact

- SB09 manager uses durable incidents, recovery, and subprocess messages.
- SB10 projectors use event/projection stores.

## Validation Depth

- Validate with persistence transaction tests, idempotency tests, event sequence tests, outbox retry tests, ledger tests, projection offset/dead-letter tests, replay tests, and dependency scans.

## Architecture Invariants That Must Hold

- Runtime does not reference EF.
- Events are append-only.
- Outbox is transactionally tied to event append.
- Projection state is derived, not authoritative runtime state.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Implement EF entities/mappings.
2. Implement unit-of-work transaction.
3. Implement event store and global/root sequences.
4. Implement outbox.
5. Implement artifact ledger.
6. Implement projection stores, offsets, and dead letters.
7. Implement idempotency indexes.
8. Add replay and concurrency tests.

## Refactoring Review Checkpoint

- Split EF entities from repositories/projectors.
- Keep SQL/provider-specific behavior in Persistence.
- Verify no UI queries use persistence implementation types.

## Required Tests / Proof

- Transaction rollback/commit tests.
- Unique idempotency tests.
- Event sequence tests.
- Outbox retry tests.
- Artifact ledger append tests.
- Projection offset/dead-letter tests.
- Replay tests.

## Search Proof

- Search Runtime for persistence implementation references.
- Search UI for persistence entity references.
- Search old observation service symbols.

## Stop And Report Conditions

- Stop if Runtime must reference DbContext or EF entities.
- Stop if UI or Application starts using persistence rows as runtime truth.
- Stop if event/outbox cannot commit atomically.

## Do Not Do

- Do not rebuild query-first observation as source of truth.
- Do not let projection stores mutate runtime state.
- Do not expose EF entities to UI.

## Acceptance Checklist

- [x] Runtime ports implemented.
- [x] Event/outbox transaction tests pass.
- [x] Artifact ledger tests pass.
- [x] Projection store tests pass.
- [x] Replay/dead-letter tests pass.

## Proof Required

- Test output.
- Persistence migration summary.
- Dependency scan.

Proof is recorded in `repo://codex/bundles/process-module-architecture-v3/proof/SB08/manifest.md` and `repo://codex/bundles/process-module-architecture-v3/proof/SB08/semantic-invariants.md`.

## Browser Validation Logging

- Browser validation is not required because UI behavior is not implemented.

## Progression Gate

- Satisfied. SB09 and SB10 may proceed after persistence tests and event/outbox integrity proof passed.

## Suggested Agent Prompt

Execute SB08 from `codex/bundles/process-module-architecture-v3/subbundles/08-persistence-event-outbox-ledger-projection-stores`. Implement durable persistence behind runtime ports without leaking EF into Runtime or UI.

## Handoff Notes For Next Bundle

SB08 implemented `ProcessPersistenceDbContext`, `EfProcessRuntimeUnitOfWork`, `EfProcessRuntimeEventStore`, `EfProcessOutboxStore`, `EfProcessArtifactLedgerStore`, and `EfProcessProjectionStore`. Event replay uses global sequence and root-run sequence order. Runtime command idempotency is keyed by `(RunId, CommandId)`. Projector offsets are keyed by `(ProjectorName, ShardKey)` and save monotonically. Replay consumers should use `IProcessRuntimeEventReplayStore` for global or root-run replay and use projection dead letters for failed projector events.
