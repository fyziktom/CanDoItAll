# SB03 Canonical Provider Snapshot Architecture

## Runtime data flow

```mermaid
flowchart LR
    DB["Workspace_ProviderProfiles"]
    Loader["No-tracking DB loader + explicit synthetic fallback"]
    Snapshot["Singleton immutable provider snapshot"]
    Lease["O(1) provider lease capture"]
    Prep["Scoped preparation service"]
    Blueprint["Bounded immutable per-agent blueprint"]
    Dispatch["Per-dispatch credential and runtime materialization"]

    DB --> Loader
    Loader --> Snapshot
    Snapshot --> Lease
    Lease --> Prep
    Prep --> Blueprint
    Blueprint --> Dispatch
```

The file workspace catalog is deliberately absent from the runtime-provider source
edge. Preparation still consumes one coherent catalog snapshot for the agent,
capabilities, and memory, but the provider itself comes from the canonical provider
snapshot.

## State and concurrency policy

| Concern | Policy |
| --- | --- |
| Read | `Volatile.Read` of immutable provider state plus dictionary lookup; existing runtime identity is checked through its short in-memory state lock |
| Initial rebuild | Serialized by `SemaphoreSlim`; database work happens outside the publication lock |
| Publication | Short lock; publish only if profile identity and publication generation still match |
| Profile switch | Advance generation and atomically publish empty `NotReady` |
| Save | Reload committed database row, create immutable lease, atomically replace one entry |
| Delete | Atomically remove the entry; no catalog fallback |
| Projection failure | Atomically publish empty typed `Faulted` state |
| Cancellation | Initializer stays `NotReady`; no partial collection is published |

## Lifetime boundaries

- Canonical provider snapshot: singleton and database-profile-generation-bound.
- Database loader and mapper: scoped; `DbContext` exists only during a load.
- Preparation cache/service: scoped and bounded by typed
  `AgentExecutionPreparationKey`.
- Blueprint: immutable configuration descriptor only.
- Credential resolution, provider clients, tools, sessions, authorization, approvals,
  and request context: per dispatch and never part of snapshot state.

## Readiness

`AppDatabaseBootstrapper.EnsureCurrentProfileReadyAsync` initializes the canonical
snapshot only after database readiness/seeding. Preparing a pending target profile
does not warm or activate it; profile activation remains restart-only.

## Source-of-truth decision

The database row is canonical. Catalog providers remain useful to legacy standalone
hosting and UI/import projection, but the integrated registry is not registered as a
runtime provider source. A missing database provider is a real deletion and cannot be
reconstructed from stale catalog data.

## Residual risks

- The startup timing is diagnostic, not a statistically controlled benchmark; SB05
  must own the performance pass/fail decision.
- Post-commit observer ordering is source-asserted and exercised through startup/DI,
  but a dedicated database mutation integration matrix would strengthen later
  regression coverage.
- A projection failure intentionally faults all provider captures. This is safer than
  serving split truth, but it requires restart/reinitialization after the underlying
  mapping/data defect is fixed.
- The final Composition/full-tree build must be rerun after concurrent SB04 context
  work reaches a coherent state.
