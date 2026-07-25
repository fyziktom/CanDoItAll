# C# Pattern Selection Records

## ADR-01: Dedicated Table Over Generic Projection Payload

- Decision: store process-run records in a dedicated table.
- Reason: common filters, ordering, paging, dispositions, timestamps, project/definition IDs, and narrative state need indexed scalar columns. Generic JSON projection keys force broad reads and deserialization.
- Rejected: another generic projection kind; it preserves the current bottleneck and has weak schema evolution.

## ADR-02: ID References Without ORM Relationships

- Decision: store IDs and bounded strongly typed JSON arrays without navigations.
- Reason: historic reads should not join canonical tables and reference metadata is available from cached services.
- Guard: C# contracts use typed IDs/value objects where available; JSON persistence does not permit string dictionaries in application code.

## ADR-03: Two-Stage Finalization

- Decision: deterministic hard facts first, structured narrative asynchronously second.
- Reason: runtime completion and projection reads must not depend on LLM latency or availability.
- Guard: explicit narrative states, lease, attempts, masked error, retry timestamp, and idempotent updates.

## ADR-04: Do Not Treat Manager Attention As An Ending Transition

- Decision: retain the strongly typed `Escalated` record disposition, but do not seed it from `ManagerLoopBudgetEscalated`.
- Reason: the current manager-loop event leaves the canonical run active/`NeedsAttention`; persisting it as an ending record would corrupt historical semantics. A future explicit runtime ending transition must emit its own event and define continuation/supersession rules before this disposition is produced.
- Rejected: globally classify `Escalated` as terminal or infer finality from a manager attention event, either of which would break existing runtime behavior and tests.

## ADR-05: Batch/Projection Reads Over EF Parallelism

- Decision: reduce round trips with SQL-shaped batch queries and stored aggregates.
- Reason: concurrent operations on the same scoped `DbContext` are invalid; `Task.WhenAll` would trade slowness for nondeterministic failures.

## ADR-06: Cohesive Top-Level Services

- Decision: add small finalizer, assembler, query, evidence, and narrative types.
- Reason: the existing query service is already oversized. New partials or another multipurpose manager would worsen cohesion and testing.
