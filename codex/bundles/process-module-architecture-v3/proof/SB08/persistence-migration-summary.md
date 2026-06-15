# SB08 Persistence Migration Summary

## Scope

SB08 adds a provider-ready EF Core persistence model for the new Process runtime in `CanDoItAll.Processes.Persistence`. It does not wire the model into the application `AppDbContext` or create an app-wide migration because runtime composition, deployment wiring, and UI integration are later subbundles.

## Provider And Context

- EF Core package: `Microsoft.EntityFrameworkCore` 10.0.4.
- PostgreSQL provider package: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2.
- Context: `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceDbContext.cs`.
- Configuration source: `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs`.

## Tables Declared By The EF Model

| Table | Purpose |
| --- | --- |
| `process_runtime_states` | Durable runtime state snapshot per run. |
| `process_runtime_steps` | Step state rows owned by a runtime state. |
| `process_dispatch_claims` | Dispatch claim leases and ownership state. |
| `process_strategy_result_receipts` | Applied strategy result receipts for idempotency. |
| `process_runtime_available_artifact_slots` | Runtime artifact slot availability. |
| `process_runtime_events` | Append-only runtime event stream with global and root sequence numbers. |
| `process_outbox_messages` | Transactional outbox rows tied to runtime events. |
| `process_artifact_ledger_events` | Artifact ledger rows tied to causal runtime events. |
| `process_runtime_idempotency_keys` | Runtime command idempotency results. |
| `process_projection_snapshots` | Derived projection snapshots. |
| `process_projector_offsets` | Projector offset checkpoints per shard. |
| `process_projection_dead_letters` | Projection dead letters for replay/retry triage. |

## Required Keys And Indexes

| Entity | Constraint |
| --- | --- |
| `ProcessRuntimeIdempotencyEntity` | Primary key `(RunId, CommandId)`. |
| `ProcessDispatchClaimEntity` | Unique index `(StepInstanceId, ClaimToken)`. |
| `ProcessStrategyResultReceiptEntity` | Unique index `(StepInstanceId, StrategyId, IdempotencyKey)`. |
| `ProcessRuntimeEventEntity` | Unique index `EventId`; unique index `(RootRunId, RootSequence)`. |
| `ProcessOutboxMessageEntity` | Unique index `(EventId, SubscriberKind)`. |
| `ProcessArtifactLedgerEventEntity` | Unique index `(SlotId, LedgerEventId)`. |
| `ProcessProjectionSnapshotEntity` | Primary key `(ProjectorName, ProjectionKey)`. |
| `ProcessProjectorOffsetEntity` | Primary key `(ProjectorName, ShardKey)`. |
| `ProcessProjectionDeadLetterEntity` | Primary key `DeadLetterId`; lookup index `(ProjectorName, ShardKey, GlobalSequence)`. |

## Validation

- Model metadata test `Persistence_model_declares_required_unique_constraints` verifies the required unique constraints.
- `bundle://proof/SB08/scans/persistence-package-model-summary.txt` captures provider package, DbContext, table, and index declarations.
- CodeAnalytics snapshot `snap-20260615203450-71eef9ce` discovered `ProcessPersistenceDbContext`, 12 EF entity types, 0 persistence diagnostics, and no dependency cycles.
