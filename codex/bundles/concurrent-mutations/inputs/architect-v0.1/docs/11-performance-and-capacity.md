# 11 · Performance, capacity and optimization plan

## Evidence and hypothesis must remain separate

There are no measured application latency/throughput results in this package. Every number below is an **illustrative model or proposed gate to be ratified after P00**, not a statement about production performance. Repository observations are SRC-03 through SRC-10; external concurrency mechanics are EXT-01 through EXT-06.

The primary cost concern is not comparing two GUIDs. It is additional sequential SQL roundtrips, wider protected state, queueing behind gates, repeated full reads, larger payloads, receipt/projection writes and work discarded after late conflicts.

## Cost decomposition

For a single operation, model latency approximately as:

`T = T_queue + N_roundtrips * RTT_db + T_sql + T_serialize + T_domain + T_commit + T_followup`

Separate `T_queue` into connection-pool wait, local-stripe wait, advisory/row-lock wait and worker admission. Separate `T_sql` from lock wait using actual measurements. Some terms overlap and batching changes the count; this is a diagnostic decomposition, not a predictive benchmark equation.

A new per-input check loop with 20 sequential calls adds roughly `20 * RTT_db` of network latency before SQL work. At an illustrative 2 ms RTT that is 40 ms; at 10 ms it is 200 ms. These are arithmetic examples, not the project's observed RTT. A bounded owner-batched query can avoid most of that multiplicative transport cost.

## Inspected hotspots and non-hotspots

| Mechanism | Potential cost | Important qualification | Proposed response |
|---|---|---|---|
| Original-stamp predicate on PK-addressed update | Small additional predicate, conflict result handling | Still need root-child atomicity and outcome semantics | Use existing row load/conditional write; avoid a second read solely for token comparison |
| 64 local semaphore stripes | Unrelated scopes queue together; process-wide shared pressure | Applies to SerializableMutationScope relational path, not every authorized Projects source path | Instrument key-class/stripe wait and hold; compare actual participating paths |
| One SQL statement per advisory key | Roundtrips scale with key count | Ordering and source-first semantics are correctness constraints | Bound key sets; evaluate supported batching without changing acquisition order |
| Repeated admission checks | Multiple source/profile lookups | Recheck can enforce current authority and must not be removed blindly | Batch independent facts; reuse only within a proven held scope |
| Full hierarchy/structure reads for narrow checks | Rows, allocations and cross-owner work grow with portfolio | Existing GetSummary/GetMany alternatives may already suffice | Add/use narrow owner reads, not dashboard APIs as validators |
| Whole phase/option replacement | Quadratic identity matching and unnecessary writes | Small lists may be immaterial; identity validation must not weaken | Profile, then use validated maps/sets and explicit intents |
| Receipt and search participation | Extra writes, WAL, indexes, commit work | Durability is not free and a same-transaction index increases coupling | Minimal payload; benchmark A/B projection choice; owner-local retention |
| Full basis per agent command | Serialization, token count, false invalidation, LLM replanning | Most observed facts are not material dependencies | Trusted bounded required basis and selective immutable references |
| Realtime refresh bursts | Many redundant reads and rerenders | Notifications not required for correctness | Optional coalescing, same-target invalidation and independent read lanes |
| Full-snapshot draft history | Per-circuit memory and GC pressure | Dirty/live/submitted copies may be large | Bounded snapshots; immutable shared base and explicit changed fields where justified |

## Stripes and false contention

For uniformly distributed independent single-key operations, the expected occupied stripes among `n` simultaneous operations in a 64-stripe design is `64 * (1 - (63/64)^n)`. At n=32 this is approximately 25.3 stripes; at n=64 approximately 40.6. This illustrates possible collision pressure, **not actual queued time or a measured performance loss**. Multiple keys, skew, differing hold times and runtime hash distribution change the situation. Real workloads require measurement.

Increasing stripe count is not a correctness fix. Removing local stripes may reveal database races that single-host tests masked. Replacing them with per-key locks introduces dictionary lifetime/refcount/eviction concerns. Keep all alternatives in P01's small spike until evidence identifies the actual problem. Do not remove the authorized source branch's deliberate ordering or add a global serialized pipeline.

## Conflict and planning economics

Under a purely illustrative Poisson arrival assumption, an edit exposed to relevant update rate `lambda` for duration `T` has probability `1 - exp(-lambda*T)` of at least one intervening change. The model is only a sensitivity aid. The useful levers are smaller **semantically valid** revision scopes, shorter publish windows and reduced unnecessary data replacement—not longer locks during thinking.

Agent planning can be much more expensive than its database checks. Measure basis age at publication, invalidating fact category, number of semantic replans and lost model/compute effort. A policy that invalidates planning on every log entry is architecturally wrong even if SQL remains fast. A token-aware caller that blindly replans forever is a throughput and cost failure.

## Workloads to benchmark

Record identical source/sibling revisions, runtime/SDK, hardware, database topology/configuration, asset mode, warmup and dataset. Use isolated PostgreSQL 18 as prescribed in SRC-12. Tests must not touch the development database, port 5032 or live customer providers.

The scenario register defines: small single editor, large phase collection, unrelated project writers, same-project contention, deliberate stripe collision, agent+human overlap, two host processes, multi-owner basis sizes, search projection failure/backlog, receipt growth and renderer draft memory. Sweep concurrency, scope cardinality and data volume independently; do not mix a dataset upgrade with the protocol A/B comparison.

Use correctness tests with deterministic barriers separately from load tests. For load, record p50/p95/p99, sample counts and confidence/variation, throughput, rejection rate, SQL commands and rows, payload bytes, CPU/allocations, connection waits, lock wait/hold, transaction time, WAL and queue lag. Report cold and warm behavior separately. A single sample is not a speedup claim.

## Candidate acceptance gates (ratify in P00)

| Area | Initial review proposal | Decision meaning |
|---|---|---|
| Ordinary edit read | No global/project-portfolio scan; bounded by requested aggregate | Structural requirement, not an absolute millisecond SLO |
| Uncontended protected save | Aim for no more than 15% p95 regression against the same current path, while satisfying all new guarantees | Candidate investigation threshold; not permission to weaken checks |
| Disjoint-project throughput | Investigate more than 10% regression at matched concurrency | Requires lock-class/stripe evidence before acceptance |
| Required basis queries | No per-item independent owner calls; count grows by explicit owner batches, not by every input row | Bound oversized requests explicitly |
| Lock critical section | Zero human/LLM waits or external HTTP/storage IO; bounded SQL/traversal only | Hard architectural gate |
| Receipt/projection payload | Minimal result/evidence; no unconditional whole-aggregate copies in generic logs | Retention and capacity review mandatory |
| UI during save/conflict | No loss of typing or unrelated-editor refresh; report p95 interaction latency and allocation change | Correctness first; latency target set from baseline |
| Replans/retries | Explicit bounded attempts, deadline and operator escalation | No infinite retry loop even under continuous edits |
| Multi-host correctness | Same protected guarantees as single host | Local test locks cannot be sole proof |

The 15% and 10% figures are **proposed review thresholds**, not hard-coded production settings or published service levels. If the baseline is very small, absolute latency and measurement noise must also be considered. An increased latency can be an acceptable explicit trade-off for a stronger guarantee; record the decision rather than hiding it.

## Query and index policy

A PK-targeted update normally need not add a standalone index on the new equality token merely because it appears in the predicate. Inspect the actual plan and lookup pattern. Additional receipt uniqueness and status/retention queries do need deliberate indexes. Existing Projects UpdatedAt is indexed; do not promise a particular PostgreSQL storage optimization merely because a new token is unindexed. Avoid broad changes to schema/indexes without query evidence.

Only immutable metadata or appropriately scoped facts may be cached. Current authority cannot be cached across an uncontrolled revocation interval. Transaction-local reuse is legitimate only when the fact remains protected and all relevant writes participate. A notification-based cache invalidation protocol is not a replacement for final conditional publication.

## Capacity estimates

Use explicit parameters: `daily_operations * retained_days * average_minimal_receipt_bytes`, plus measured index/WAL overhead, for a first receipt budget. For example 50,000 operations/day * 30 days * 1 KiB is about 1.43 GiB of raw payload before indexes and overhead. This is a synthetic workload, not a forecast for CanDoItAll. Model compact dedupe retention separately from full detail retention.

Outbox arrival rate must remain below sustainable drain rate over the operational window. Expose backlog age and use bounded retries/coalescing/backpressure. Do not silently drop required projection work to keep the primary save fast.

## Optimization order

First remove accidental full reads and N+1 validation. Next shorten lock lifetime and reduce needless field/collection writes. Then batch allowed same-owner checks and optimize receipt/projection payload. Only afterward consider changing lock granularity/isolation or splitting revision scopes, because those affect proofs and writer participation. Do not make an application-wide infrastructure refactor a prerequisite for a local safe CAS repair unless measured or correctness evidence requires it.
