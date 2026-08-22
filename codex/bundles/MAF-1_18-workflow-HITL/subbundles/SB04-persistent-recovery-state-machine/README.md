# SB04 — Persistent Checkpoint and Recovery State Machine

## Status

Proven

## Outcome

Persist native MAF checkpoints and external response operations in PostgreSQL/EF, make response acceptance and resume crash-recoverable, verify exact workflow topology/version, and provide exactly-once response acceptance and deduplicated participating governed effects.

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

## C# Architecture Impact

This critical foundation moved response acceptance/resume ownership out of the baseline 748-line runtime manager, added a recoverable explicit operation state machine, and introduced focused PostgreSQL stores plus governed executor replay protection. `WorkflowRuntimeManager` is now 738 lines; its public entry points are thin factory delegates and response submission is a one-line delegate. The compatibility construction path is test-only. No new persistence responsibility was appended to the 3,133-line `PersistentWorkflowStores.cs` cluster.

The governing design is recorded in `../../architecture/00-csharp-current-state-inventory.md` through `../../architecture/04-csharp-testability-plan.md`. SB04 remains locked until CP-WB1 proves SB03; CP-WB2 is its closure gate.

## Boundary Ownership

- Models own operation, lease, checkpoint-payload, request-version/state, and executor-invocation values/records.
- Workflows.Abstractions owns business-specific checkpoint, operation, continuation/boundary, and dedup ports.
- Workflows.Core owns pure operation-transition and compatibility policy.
- Workflows.Runtime owns one external-response continuation coordinator; `WorkflowRuntimeManager` delegates rather than duplicating lifecycle ownership.
- WorkflowExecutors.Core owns the stable invocation-key factory and decorator around the existing invoker.
- Modules.AgentFramework owns focused EF entities/configurations/stores in separate files and production composition.
- The PostgreSQL migration project owns generated migration/snapshot changes.

## Dependency Direction

No new production project/reference was added. Core/Runtime remain free of MAF, EF, and Npgsql; persistence implements inward-facing ports; Infrastructure does not reference the module. Final snapshot `snap-20260821044013-44e660f5` covers 9 projects and 478 documents, reports no project cycle, and retains exactly the same two named baseline non-project cycles as `snap-20260820220112-5cb38069`. Executable composition proof supplements the static DI analysis.

## Pattern Decision

- State: explicit response-operation enum and pure legal-transition policy.
- Business CAS/lease port: conditional PostgreSQL updates and unique constraints, not a generic repository or in-process lock.
- Decorator: governed executor replay/dedup around the existing invoker, with a propagated participant idempotency key.

Rejected alternatives include boolean/timestamp state inference, read-then-save coordination, unbounded retries, generic Unit of Work abstractions, driver-only dedup, and claiming arbitrary external exactly-once behavior. Full records are in `../../architecture/03-csharp-pattern-selection-records.md`.

## Testability Contract

Transition policy, continuation coordinator, lease policy, invocation-key factory, and dedup decorator are directly constructed with recording fakes and `FakeTimeProvider`; they do not require a manager, host, or DbContext. Persistent stores are tested against real PostgreSQL with migrations. Crash points are injectable before claim, before delivery, after delivery/before finalization, and at participating effect commit.

## Partial Class Policy

No handwritten production partial or nested service is allowed. Each new EF entity/configuration/store is a focused top-level type in a separate file. Generated migration/designer partials are allowed. `PersistentWorkflowStores.cs` and `WorkflowRuntimeManager` must shrink/delegate rather than grow.

## Architecture Proof Required

- CP-WB1 remains Proven before any SB04 implementation;
- before/after project/dependency/cycle evidence and framework isolation;
- direct unit and negative tests for transition, continuation, lease, and dedup responsibilities;
- source assertions that the runtime manager no longer owns response accept/CAS/backend/finalize and the old persistence cluster did not absorb new stores;
- executable persistent composition and exactly-one-decorator smoke;
- real PostgreSQL migration, constraints, ordinal ordering, CAS/lease race, takeover, stale-owner, reconstruction, and legacy proof;
- all crash windows, cancellation, consecutive-wait, exact-version/topology, missing/corrupt checkpoint, and participating-effect dedup proof;
- precise guarantee language: exactly-once response acceptance and deduplicated participating governed effects, not arbitrary external exactly once;
- CP-WB2 reviewer decision and downstream SB05 unlock only after governed proof is complete.

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

Proven on 2026-08-21. CP-WB2 strict architecture re-review is Pass and SB05 is
dependency-ready.

- entities/migration: focused top-level checkpoint session/payload, external-request
  boundary, external-response operation, and executor-invocation entities,
  configurations, and stores are composed in `Modules.AgentFramework`; migration
  `20260821021747_AddWorkflowHitlRecovery` is present and
  `dotnet ef migrations has-pending-model-changes` reports no pending model change;
- checkpoint ordering/hash: PostgreSQL allocates typed checkpoint identity and ordinal
  atomically, returns the index in ascending commit order, and verifies payload hashes;
- operation state/CAS: persistent create/replay/conflict, single active claim, staged
  boundary commit, cancellation, and terminal-state immutability are proven;
- lease/recovery: heartbeat, stale-owner rejection, bounded expired-lease takeover, and
  retryable recovery are proven against direct policy tests and real PostgreSQL;
- exact-version/topology: continuation reloads the exact workflow version and fails
  closed on identity, topology, missing, corrupt, or legacy checkpoint incompatibility;
- deduplication: the existing executor invoker is decorated exactly once; completed
  participating effects replay without a second inner invocation, live claims conflict,
  and input-hash mismatch fails closed;
- legacy behavior: legacy metadata-only waits remain inspectable but non-resumable and
  are never restarted from initial input;
- crash simulations: accepted-before-claim, claimed-before-delivery,
  delivered-before-finalize, and participating-effect-before-operation-completion paths
  are covered;
- tests/counts: the immutable 26-clause Unit selector passed 419/419; the exact
  three-class Integration selector passed 16/16, including 15 real PostgreSQL facts and
  one production-composition fact; ten affected Release project builds and the final
  post-fix Release Unit build passed with zero warnings and zero errors;
- architecture: `WorkflowRuntimeManager` is 738 lines versus the 748-line baseline,
  public entry points are thin factory delegates, response submission is a one-line
  delegate, the compatibility factory path is test-only, and snapshot
  `snap-20260821044013-44e660f5` reports 9 projects, 478 documents, no project cycle, and
  only the same two named baseline non-project cycles;
- governance: the upgraded-package scanner passes at stable `1.18.0` and preview
  `1.18.0-preview.260818.1`; governed artifacts are under `proof/SB04`;
- blockers/deviations: no closure blocker remains. Integration assets required one
  explicit restore from stale 1.17 test assets, and the first sandboxed Module build
  required an authorized retry for sibling Components generated-asset cache writes;
  passing reruns and the retained progression evidence are recorded in `proof/SB04`.

The proven guarantee is exactly-once response acceptance and deduplicated participating
governed effects. It does not claim exactly-once behavior for an arbitrary external
system.
