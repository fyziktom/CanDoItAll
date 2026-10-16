# Performance and operational risk plan

Status: design; no new benchmark result is claimed.

## Hard cost constraints

- Zero model, external network or filesystem work inside the owner transaction;
  the bounded PostgreSQL calls are the intended transactional I/O.
- At most a fixed number of owner-batched basis queries, never one query per fact.
- No aggregate-wide revision for chat append, activity or process progress.
- No background timer polling every editor or every project. Optional invalidation is
  coalesced by target and is advisory; a dropped notification cannot enable a stale write.
- One immutable submitted draft per in-flight operation and one local live draft.
  Release acknowledged generations. Do not retain every edit or render snapshot.
- Validate request sizes before acquiring locks; reject explicitly rather than truncate.
- Store minimal operation outcomes and compact identity evidence. Do not retain full
  content/proposals in a generic receipt when existing governed storage owns them.

## Measurement protocol

Use the same source baseline, sibling commits, SDK, database configuration, dataset,
machine and asset mode for A/B measurements. Warm up before collecting distributions;
report sample size and cold behavior separately. Correctness tests use deterministic
barriers and do not use elapsed time as proof of ordering.

Start with 100 measured operations after 20 warmup operations per uncontended sample,
three repeated samples, and bounded 1/8/32 writer contention runs. These are investigation
sizes, not a production load forecast. Record p50/p95/p99, successful and rejected
operations, throughput, SQL statements, rows, payload bytes, allocation/GC where
available, transaction duration, connection wait and lock wait/hold. Report unavailable
metrics explicitly. Do not infer WAL or server CPU from client elapsed time.

Compare small projects (0/5 phases) with 100/1000 phases/options, disjoint projects with
same-project writers, and same-process with two-process writers. Include deliberately
colliding local semaphore keys, a long-held advisory lock, delayed commit response,
projection failure, and receipt retention/replay. Payload and basis size boundaries need
both exactly-at-limit and one-over-limit cases.

Initial investigation thresholds: an uncontended protected save p95 increase above 15%
or disjoint-project throughput loss above 10% requires explanation and redesign or a
documented accepted cost before closure. Tiny baselines must include absolute differences
and variation. Neither number permits removing a correctness check. No absolute latency
SLO is invented without an observed baseline and deployment requirement.

The first optimization is removing accidental scans and quadratic collection matching.
Use validated ID maps/sets with linear matching. Next reduce repeated transaction-local
reads and projection payload. Change stripe count, isolation or lock granularity only
after measurement and independent multi-host correctness proof.

## Risk register

| Risk | Failure | Prevention and required negative proof |
| --- | --- | --- |
| Hidden writer | Another tool bypasses CAS | Source/caller inventory; missing-stamp rejection through each enabled boundary |
| Wrong aggregate | Every runtime log conflicts with editing | Definition-only revision; independent activity/graph/task tests |
| Scalar command replaces collections | Agent deletes phases/options | Dedicated scalar intent; retain child identity/content regression |
| Torn read | Root token describes different children | Coherent read transaction; barrier between root/child reads |
| Child-only write | Root token stays valid | Root stamp advancement; stale aggregate save rejected |
| ID substitution | Foreign/deleted rows recreated | Distinct owned-ID validation before mutation |
| Late search callback | Older projection wins | Atomic participant or ordered durable rebuild; reversed completion test |
| Commit response lost | Retry duplicates effect | Atomic operation result, same identity lookup/replay; lost-response test |
| Cleanup throws | Known commit appears failed; locks leak | Separate commit classification; finally-release and fault injection |
| Key collision | Unrelated writes serialize | Measure stripe wait/hold; preserve bounded lock lifetime |
| Lock inversion | Source/owner/graph deadlock | Complete lock class/order map, opposite-order race and timeout proof |
| Snapshot before wait | Newest row not visible after acquiring lock | Current-row/conditional-write enforcement; two-connection barrier |
| Retry storm | Continuous edits cause model/SQL loops | Separate transport retry and semantic replan budgets, deadline and escalation |
| Stale authority | Snapshot setting preserves revoked access | Current claim/access at effect; revoke/takeover before publication test |
| Basis omitted | Model hides an inconvenient dependency | Trusted operation requires basis; missing material witness denied |
| Phantom membership | Plan misses a newly inserted task | Owner set witness; insertion/removal during planning test |
| Partial refresh | New facts mixed with stale derived plan | Invalidate dependent proposal and approval; fresh admitted attempt |
| Dirty reload | User loses local typing | Explicit conflict state and submission/live reconciliation; delayed ack browser test |
| Cross-editor response | Old save updates new selection | Generation and operation identity checks |
| Restore ABA | Old request becomes valid after backup restore | Durable dataset namespace outside restored history; restore rehearsal |
| Receipt pruning | Expired ID executes again | Advertised horizon, compact evidence/expiry refusal; retention boundary test |
| Import revives authority | Source tokens/claims become executable | Fresh target identity; imported receipts/history cannot authorize |
| Old binary/client | New schema but unconditional writes | Convert or reject enabled writer; mixed-version negative test |
| External uncertainty | Remote effect retried blindly | Capability-specific idempotency/conditional write/status lookup; explicit uncertain state |
| Contract coupling | Core references Projects/MAF/EF | Neutral contracts and owner adapters; evaluated graph/boundary tests |
| Test illusion | InMemory/local locks hide distributed races | PostgreSQL 18, independent contexts and host processes |
| Disclosure | Conflict/replay leaks current sensitive content | Current read authorization and bounded redacted outcome |
| Proof drift | Tests built before last source change | Standard outputs rebuilt; source identities and exact discovery recorded |

## Bounds and retention decisions

Use existing owner limits where they exist. Any new batch/basis/phase limit must be
backed by the measured supported dataset and exposed as a typed validated option with
an explicit error. Do not silently introduce a low limit that rejects existing projects.
Until a chosen limit and retained-data preflight pass, that mutation cannot advertise
bounded production cost. Replan defaults should be conservative and finite; budget
exhaustion pauses/requires review rather than discarding work or forcing publication.

For receipt capacity, report operations/day multiplied by retained minimal bytes and
days, plus measured index overhead; these inputs are unknown until the workload is
measured. Full detail and compact deduplication retention are separate. Garbage collection
must not race an in-flight lookup/commit and must respect paused/recoverable checkpoints.

## Local fixture and cleanup plan

No isolated PostgreSQL test endpoint was configured at discovery. Existing containers
include the ordinary PostgreSQL instance for port 5032 and an unrelated application;
neither is a fixture. Provision one labelled disposable PostgreSQL 18 server only when
persistence tests begin, record its exact identity/ports/mounts before use, reuse it
across all related lanes through existing database leases, and remove only those owned
resources after their last required use. Keep credentials out of evidence.

Owned browser/host processes must use an isolated endpoint and be stopped before final
cleanup. Preserve the ordinary application, installed MCP tools and useful unrelated
containers. Keep concise reviewed proof, not duplicated compiled output trees.
