# Monitoring Events Snapshots And UI Projections

Projection and live/history implementation must also follow `architecture/19-dotnet-performance-guardrails.md`. Monitoring must be event-first, bounded, asynchronous, and snapshot-backed without blocking runtime state transitions.

## Design Intent

Monitoring must not slow process execution. Runtime writes typed events and minimal state. Asynchronous projectors build snapshots and history read models. UI reads projections, not runtime internals or EF entity graphs.

The current live-process UX direction is preserved, but the backend becomes event-first instead of cache/query-first.

## Model Concepts

Monitoring concepts:

- `ProcessRuntimeEventEnvelope`: durable event written by runtime.
- `ProcessOutboxMessage`: reliable external notification or projector trigger.
- `ProjectionWorker`: asynchronous consumer with durable offset.
- `ProjectionOffset`: per-projector last processed event marker.
- `ProjectionDeadLetter`: failed projection event and diagnostic reference.
- `LiveProcessSnapshot`: current run card, step summary, manager incident summary, metric summary, and freshness.
- `RunDetailProjection`: complete run view from events and artifact ledger.
- `TimelineProjection`: ordered event history with time-range filtering.
- `DefinitionCanvasProjection`: design-time canvas read model.
- `RuntimeCanvasProjection`: runtime graph state read model.

## Event And Projection Flow

1. Runtime validates transition.
2. Runtime writes state change and event in one transaction, or writes event plus outbox under a reliable transaction boundary.
3. Outbox wakes projection workers.
4. Projection workers process events by offset.
5. Projectors update current snapshots and append/update historical projections.
6. UI queries current snapshot cache or history projections.
7. Force refresh bypasses memory cache and reads projection storage; it does not query runtime internals.

## Live And History Semantics

Live Processes:

- Reads current run snapshots plus bounded recent event projections.
- Includes active runs even if the run started before the selected history window.
- Filters completed historical events by selected time range.
- "Last hour" means event timestamp within the last hour unless active-run inclusion explicitly applies.
- Force refresh bypasses memory cache but still reads projections.
- Snapshot response includes freshness, observed-at UTC, source max event sequence, and projector lag.

History:

- Reads historical projections by explicit time range.
- Does not include stale older events when a bounded window is selected.
- Supports pagination and stable cursoring.
- Can link to restricted raw diagnostics only through authorized evidence links.

Canvas:

- Definition canvas reads definition projection.
- Runtime canvas reads runtime projection.
- Canvas renders branch routes, subprocess boundaries, artifact slots, and current state from projection fields.
- Canvas does not calculate runtime truth.

## Snapshot Storage Rules

Current snapshots:

- keyed by root run ID, run ID, project ID, definition ID, and projection kind,
- overwritten by projectors,
- include event sequence/version,
- may be cached in memory,
- expire by policy but can be rebuilt from event store.

History projections:

- append-oriented or versioned rows,
- indexed by UTC timestamp, run ID, step ID, event type, severity, incident class, and sensitivity,
- queryable by time range,
- replayable from event store.

## Invariants

- Monitoring observers never block dispatcher strategy execution.
- Projection failure does not roll back runtime state after runtime commit.
- Projection lag is visible to UI.
- Dead-letter events are visible to operators.
- UI cannot mutate runtime state through projection models.
- Time-range filters are applied at projection/query boundary, not only after rendering.
- Raw diagnostics remain restricted evidence.

## Failure Behavior

| Failure | Behavior |
| --- | --- |
| Projector fails on event | Record dead-letter, keep offset before failed event or use poison-event policy, alert operator. |
| Projection lag grows | UI shows stale marker and operator alert; runtime continues. |
| Snapshot missing | Rebuild from event store or return explicit projection-unavailable response. |
| Event payload schema unknown | Dead-letter with schema diagnostic; do not discard. |
| Cache stale | Force refresh reads projection storage and updates cache. |
| History query window invalid | Return validation error with accepted ranges. |

## Boundary Rules

- Runtime emits events; it does not build UI cards.
- Projectors read events and artifact ledger; they do not issue runtime transitions.
- UI reads application projection services; it does not query EF runtime tables directly.
- Projection services can expose domain facets produced by drivers, but generic UI contracts remain stable.
- Projection DTOs and read-model contracts live in `CanDoItAll.Processes.Projections`.
- Projection storage, offsets, dead letters, and replay are implemented behind persistence ports described in `architecture/12-runtime-persistence-event-store-and-outbox.md`.
- UI surface inventory and allowed data sources are defined in `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`.

## Test Implications

- Event tests verify envelopes, causation/correlation, sensitivity, schema versions, and append-only behavior.
- Projection tests verify offsets, replay, dead letters, current snapshots, historical projections, freshness metadata, and force refresh.
- Live/history tests prove active-run inclusion and strict time-window filtering.
- UI/component tests verify projection-only access and no dependency on runtime internals.
- Playwright tests later verify Live Processes last-hour behavior, run detail, canvas runtime view, and restricted diagnostic links.
