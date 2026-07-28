# C# Pattern Selection Records

## PSR-01: Partitioned sequenced stream

- Context: multiple modules need immediate ordered operational feedback; future SSE needs resumable sequence semantics.
- Selected: a singleton-safe bounded partitioned ring/log with typed keys, primitive-owned sequence, fan-out cursors, explicit gaps/tombstones, terminal replay TTL, global partition limit, and cancellation-aware async reads.
- Rejected: synchronous multicast events because handlers can block/throw/reorder outcomes.
- Rejected: `ConcurrentQueue` snapshots because they lack subscriptions, isolation, strong partitions, and gap semantics.
- Rejected: MQTT/OPC UA/SSE in-process because transport concerns and authorization would leak into producers.
- Rejected: a global `object` event bus because type discovery and access control become runtime/string conventions.
- Consequence: slightly more contract code and cleanup policy, but deterministic cross-scope projection and future adapter boundaries.

## PSR-02: Domain-specific wrapper

- Context: a generic primitive alone permits accidental cross-module coupling.
- Selected: Agent Framework exposes a coordinator/reader and per-operation leases. Producers report unsequenced facts; the coordinator owns binding/phase/terminal CAS.
- Rejected: modules directly publishing arbitrary generic events.
- Consequence: producers/consumers depend on an intentional domain contract; other modules can define their own wrapper later.

## PSR-03: Immutable revisioned blueprint

- Context: current warm pool contains only agent metadata; live runtime construction is expensive.
- Selected: prepare a bounded immutable per-agent map keyed by typed profile/workspace/agent identity and one store-owned catalog data revision plus profile generation/provider fingerprint; materialize live state per run.
- Rejected: object pool of MAF agents/provider clients/tools due to secret, authorization, session, disposal, and stale-capability leakage.
- Rejected: string-key cache due to refactor fragility and unclear invalidation.
- Consequence: avoids repeated catalog/profile assembly while preserving request-specific correctness.

## PSR-04: Single-flight with generation fence

- Context: concurrent warm/acquire calls should not duplicate work, but caller cancellation must not cancel everyone.
- Selected: scoped-service lifetime CTS owns per-key load tasks; atomic generation capture, per-waiter `WaitAsync(cancellationToken)`, and commit only when generation matches.
- Rejected: `Lazy<Task<T>>` capturing the first caller token.
- Consequence: independent waiter cancellation and deterministic stale-result rejection.

## PSR-05: Runtime-first context snapshot

- Context: project/process pages already hold useful read projections.
- Selected: owning module constructs a fully immutable typed attachment and atomically replaces it with its prompt fragment through the existing context-registry lock; the registry captures it once for a request.
- Selected: monotonic publication revision, authorized content/selection fingerprint, coverage fingerprint, and database-profile generation are separate typed projection values; none is a canonical mutation token.
- Selected: lookup matches captured source/scope/workspace, contributor, kind, exact concrete type, current profile generation, freshness, and coverage. It returns a typed ineligibility reason rather than choosing by type or falling through to storage.
- Selected: covered snapshot reads perform no storage; a deeper canonical read requires an explicit typed source selection.
- Selected: the existing transient-context lease carries attachment envelopes to runtime-tool construction, and the invocation digest separately binds envelope identity, publication revision, content fingerprint, coverage fingerprint, profile generation, and freshness bounds for approval continuation.
- Rejected: agent runtime querying UI component state directly.
- Rejected: an independent module snapshot registry/cache, copying into mutable state, or writing snapshots back to canonical stores.
- Rejected: hidden storage fallback on coverage/freshness failure, treating `UpdatedAtUtc` as an
  optimistic-concurrency token, or passing any snapshot payload/stamp to a write path.
- Consequence: fast prompt/tool context with explicit eligibility and bounded staleness that
  cannot mutate after capture. Writes remain entirely inside current canonical application
  services; true Project Structure row-version concurrency is a separate cross-writer concern.

## PSR-06: Remove duplicate I/O before parallelism

- Context: startup rereads catalog/session and has independent provider/session reads.
- Selected: pass a prepared startup aggregate through run creation, make provider resolution consume that catalog, then overlap only proven-independent reads with explicit failure ordering.
- Rejected: `Task.WhenAll` across one store/DbContext or mutable runtime composition.
- Consequence: fewer operations and safer latency improvement than broad parallelization.

## PSR-07: Compatibility event migration

- Context: many existing consumers use `ExecutionUpdated`.
- Selected: introduce typed activity first, keep existing durable log mutations unchanged, raise their compatibility notifications in isolated handlers after persistence, and move UI consumers incrementally. Activity never writes or reconstructs durable logs.
- Rejected: flag-day removal.
- Consequence: smallest safe change with measurable migration checkpoints.

## PSR-08: Metrics and proof

- Context: timing exists only for part of MAF composition and production discards it.
- Selected: correlated stage measurements and deterministic operation-count probes; artifacts report cold/warm median/p95 without tight CI time assertions.
- Rejected: one real provider call as the only benchmark.
- Consequence: cheap repeatable proof plus one final `gpt-5.4-mini` wiring sample.

## PSR-09: One activity operation per command

- Context: approval continuation is a later call and cannot safely reopen an evicted/in-memory operation after restart.
- Selected: the initial invocation ends with terminal outcome `Suspended` when awaiting approval; continuation gets a new operation bound to the same run.
- Selected: every new execution request/instrumented production entry requires an operation ID; the persisted run property alone is nullable for legacy deserialization.
- Rejected: overloading integration `CorrelationId`, metadata JSON, or keeping an operation open indefinitely.
- Consequence: exactly-one terminal/cleanup remains enforceable while run correlation survives.
