# SB04 — Persistent Checkpoint and Recovery State Machine

## Status

Prepared

## Outcome

Persist native MAF checkpoints and external response operations in PostgreSQL/EF, make response acceptance and resume crash-recoverable, verify exact workflow topology/version, and deduplicate governed side-effecting executor invocation.

## Owned requirements

RQ-016 through RQ-031 and lifecycle portions of RQ-019 through RQ-023.

## Non-goals

- public HTTP DTO/status work beyond service contracts;
- arbitrary distributed workflow durability;
- exactly-once effects in non-participating external systems;
- destructive cleanup of legacy checkpoints;
- silently converting legacy waiting runs;
- unrelated database refactoring.

## Prerequisites

SB03 passed with real checkpoint and disposed-run rehydration proof.

## Reopen triggers

- IK-09 serialization issues;
- IK-10 persistence conventions differ;
- IK-11 multi-host race;
- IK-12 executor cannot deduplicate;
- schema or state transition contradicts current store atomicity;
- a crash case can consume a response without recoverable status;
- a replay can invoke the governed side-effect probe twice.

## Exact sources and discovery

- workflow runtime contracts and transition rules;
- `WorkflowRuntimeManager.cs`;
- persistent workflow stores and EF entity configurations;
- AppDbContext/migration conventions;
- current run/event/request atomic methods;
- workflow executor invoker/approval gate;
- active-run registry;
- lifecycle/checkpoint tests;
- current database integration test fixtures.

Use the persistence model and state machine documents as decisions, then adapt names to repository conventions.

## Implementation boundary

### 1. Persistent checkpoint payload store

Implement the framework-neutral port from SB03 over EF/PostgreSQL.

Persist:

- run/workflow/version;
- backend session/checkpoint/parent IDs;
- commit ordinal;
- JSON payload and hash;
- checkpoint/compiler format versions;
- topology fingerprint;
- request linkage;
- timestamps.

Enforce unique keys and explicit ordinal ordering.

### 2. Response operation ledger

Add an immutable/auditable operation record with:

- request/run;
- idempotency and payload hashes;
- actor/correlation fields required by service input;
- state, attempt, lease, timestamps;
- safe outcome;
- result references;
- optimistic concurrency token.

Implement atomic create/replay/conflict and one active operation claim per request/run.

### 3. Recoverable manager/service sequence

Refactor response submission so it does not irreversibly mark `RespondedAtUtc` before a recoverable operation exists.

Required sequence:

1. validate and create/replay operation;
2. claim operation/run through CAS;
3. transition to running/resuming;
4. call MAF backend;
5. atomically persist resulting run/events/checkpoint/request boundary;
6. finalize operation and request;
7. release lease.

On host loss, an expired claim can be resumed from operation/checkpoint state. Do not create a second independent response.

### 4. Exact-version/topology verification

Load exact `WorkflowVersionId`, not latest active.

Verify:

- workflow/version/run/request linkage;
- compiler contract version;
- topology fingerprint;
- MAF session/checkpoint/request/port IDs;
- payload hash.

Return typed terminal/retryable outcomes.

### 5. Executor invocation deduplication

Surround governed side-effecting executor invocation with a stable key.

At minimum prove:

- one completed result is reused on replay;
- active claim prevents parallel duplicate;
- input hash mismatch fails;
- external calls receive a propagated idempotency key when supported;
- an outbox/driver-specific contract is used where existing architecture already provides it.

Do not indiscriminately cache pure LLM calls or secret-bearing results outside policy.

### 6. Migration

Add repository-conventional migration/entity configuration.

Preserve legacy data. Mark old waiting checkpoints as non-resumable by discriminator/absence, not by destructive rewrite.

## Acceptance criteria

- MAF checkpoint survives DbContext/process disposal;
- ordered index and payload retrieval satisfy MAF contract;
- same response key/payload yields same operation;
- changed payload with same key conflicts;
- parallel submissions create one active claim;
- crash after claim is recoverable;
- crash after native response delivery does not intentionally duplicate governed probe effect;
- expired lease recovery is bounded and auditable;
- exact workflow version is loaded;
- topology mismatch fails closed;
- missing/corrupt/legacy checkpoint does not mark response complete and does not restart;
- consecutive external request persists new checkpoint/request and closes prior operation;
- cancellation wins correctly in races;
- backend descriptor accurately advertises resume but not durability;
- migration and rollback compatibility are documented.

## Proof tier

Governed

## Focused validation

Unit:

- `MafJsonCheckpointStoreAdapterTests`
- `WorkflowExternalResponseOperationTests`
- `WorkflowRuntimeLifecycleRedGateTests`
- `MafWorkflowHumanInLoopTests`
- executor/deduplication tests.

Integration with real test database:

- checkpoint create/index/read;
- CAS/idempotency race;
- process/service reconstruction;
- migration up and application startup;
- legacy row read.

Required crash simulations:

1. after operation accepted, before backend call;
2. after lease claimed, before response delivery;
3. after response delivery, before final result persistence;
4. after side-effect probe completion, before operation completion.

Create `proof/SB04` governed artifacts with hashes. Redact payloads.

## Invalidation keys

IK-09, IK-10, IK-11, IK-12, IK-16, IK-17.

## Broad-gate decision

No solution-wide gate. Run affected unit and database integration projects. The migration and source remain mutable until SB05 closure.

## Closure record

Not executed.

Record:

- entities/migration:
- checkpoint ordering/hash:
- operation state/CAS:
- lease/recovery:
- exact-version/topology:
- deduplication:
- legacy behavior:
- crash simulations:
- tests/counts:
- blockers/deviations:
