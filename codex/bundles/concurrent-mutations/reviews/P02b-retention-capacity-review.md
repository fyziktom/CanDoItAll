# Retention amendment: implementation review

Status: approved for the narrow implementation below, subject to its focused proof and
parent review. This is a source/evidence review, not completed validation or a new
production capacity claim. No build, test, benchmark or database access was performed.
It supersedes the amendment's original pending-baseline wording and partial-run numbers; it does
not change a persisted recovery promise or authorize a new maintenance default.

Reviewed [amendment](../plan/P02b-retention-capacity-amendment.md),
[complete protected baseline](../validation/P02bc-protected-performance-baseline.md),
`ProjectDefinitionOperationOptions`, `ProjectDefinitionOperationMaintenance`,
`ProjectOperationEpochService`, `ProjectDefinitionReceiptReader`,
`ProjectDefinitionOperationProtocol`, `ProjectsProtectedMutationPerformanceTests` and
the 12 existing `ProjectOperationEpochTests` cases. All source names are in the Projects
module or its Integration test project unless otherwise stated.

## Decision and evidence

Proceed with configurable cadence, typed bounded cycle observations, and standalone
contiguous retirement. Keep the existing one-minute default, two-second shared cycle
budget, 64 purge batches, 128 receipt candidates and 16-MiB payload cap per batch.
Evaluate a ten-second deployment setting only after an identical mixed-load comparison.
No migration, new project reference, per-write guard, global capacity counter, new
scheduler, leader election, automatic retention reduction or dynamic promise reload is
needed for this checkpoint.

The complete pre-optimization baseline passed both Facts: 33 samples, 3,300 offered
operations, 2,151 new acceptances, 300 replays, 300 found lookups and 549 expected
same-project conflicts. All disjoint attempts accepted. Their finite closed-loop rates
were 146.04–219.31/s at eight writers and 140.28–183.71/s at 32. These are useful
synthetic evidence, not sustained arrival rates. They exceed the configured small-row
purge ceiling of 136.53/s. Same-project offered rates are not new receipt arrival rates.

Use the completed cleanup observations: 8,192 small receipts in 974.965 ms, limited by
64 batches with one row remaining; 192 large receipts in 52.825 ms, exhausting that
backlog. The earlier 823/42-ms cells remain separate historical evidence. At the current
cadence, structural ceilings remain 8,192/minute for 2,171-byte receipts,
3,648/minute for 291,319-byte receipts, and 512/minute at the 2-MiB result limit.
Neither elapsed time proves mixed-load service rate. The full run did not measure
continuous arrivals, concurrent hosted maintenance, vacuum/reuse or production payload
distributions. Canonical-length reuse is a separate optimization and does not repair
receipt storage capacity.

Current regular default retention can keep approximately 8–9 days of arrivals before
backlog because retirement runs only during daily rotation. Standalone retirement
removes that avoidable extra rotation wait: eligibility approaches 7–8 days plus
mutation, closure and cycle delay. Neither number is a hard disk bound. Backlog,
stopped hosts and long issued promises increase physical retention. The modeled
seven-day payload-only lower bounds, 1.313 GB per sustained accepted write/s for small
receipts and 176.190 GB for large receipts, remain lower bounds; table/index/TOAST
allocation and operational headroom must be budgeted separately.

## Exact minimal owner changes

1. `ProjectDefinitionOperationOptions`: make `MaintenanceInterval` an instance option,
   keep `DefaultMaintenanceInterval = 1 minute`, and validate 5 seconds through 1 minute.
   Add `MaintenanceWarningAfter`, default 5 minutes, between twice that interval and
   1 hour. Retain startup-validated `IOptions`; these settings do not enter issued epoch
   promises. The 2-second/5-second ratio is a scheduling target, not a guaranteed CPU
   duty cycle: cancellation and cleanup still have their existing bounded SQL semantics.
2. `ProjectOperationEpochService`: add `AdvanceRetirementAsync(binding, token)` and a
   small typed outcome distinguishing advanced, unchanged and busy. Change rotation's
   state acquisition to `FOR UPDATE SKIP LOCKED` before any current-epoch UPDATE lock.
   Prefer moving the existing prefix-retirement loop out of rotation into this sole
   method, avoiding two retirement implementations. Existing write-context readiness
   already invokes `RotateExpiredAsync`; it must not start retirement or purge work.
3. Replace purge's `int` result with an owner-local typed result containing confirmed
   deleted receipt count, confirmed payload bytes and bounded eligibility evidence.
   Keep all three existing DELETE statements and their exact active-binding predicates.
   This result is maintenance evidence, not a new mutation receipt protocol.
4. Keep `ProjectDefinitionOperationMaintenance` as the scheduling owner. A cycle captures
   one runtime profile/binding, rotates, advances retirement, then performs at most 64
   purge calls under one linked 2-second token. State contention does not require
   aborting useful purge of an already retired prefix. Share the concrete cycle method
   with the opt-in harness if needed; do not add a one-implementation maintenance
   interface, general dispatcher or production test hook.
5. Add owner-local Metrics instrumentation following Infrastructure's existing
   `RuntimeClaimMetrics` pattern. Adapt the frozen harness to report the actual configured
   cadence and production cycle result; retain workload version/source identity and do
   not relabel the old one-minute observations as ten-second measurements.

`ProjectsModuleServiceCollectionExtensions` already validates these options on startup.
The offline activation command also registers the same options; it need not run the
worker. No browser/API protocol, receipt codec, activation file or authoritative writer
transaction changes are required.

## Retirement transaction and lock proof

The standalone method uses a finite READ COMMITTED transaction and installs the existing
maintenance SQL deadlines before querying. Its coherent precheck requires the exact
active namespace, maintenance identity, schema and guard protocol. This is an explicit
configuration check, not authority for a later mutation.

Acquire the active namespace's state row `FOR UPDATE SKIP LOCKED`. A locked existing
row means Busy; absent/inconsistent state is an explicit failure, not Busy or Empty.
After acquiring it, read at most 128 ordered epoch rows starting immediately above
`RetiredThroughEpoch`, excluding `CurrentEpoch`. Advance only the contiguous Closed
prefix for which both issued admission-close-plus-mutation-deadline-plus-retention and
actual-close-plus-retention have elapsed against the database clock. Stop at the first
gap, Open/non-Closed state, missing close time, or live promise. Do not use current
deployment retention to reinterpret an existing row.

Update only `RetiredThroughEpoch` with a monotonic predicate and exact active-binding
predicate in that UPDATE statement; require the expected affected-row count. Do not
save a previously tracked lower state object. No receipt DELETE occurs here. Commit
retirement before a later purge transaction. A crash then leaves excess bytes, while
the existing coherent receipt reader checks the watermark before inspecting retained
payload and returns Expired. That is the required safe ordering.

Rotation retains its lock-free server-time precheck before lifecycle SHARE. If expiry
is due, order its locks as lifecycle SHARE, state UPDATE, current epoch UPDATE. Read
the current epoch from the locked state, not the precheck. A current writer's epoch
SHARE may make closure busy; it must not prevent standalone advancement of older
Closed evidence. Plain old-epoch reads do not take an epoch UPDATE lock.

Activation takes lifecycle UPDATE before closing the old Open epoch; it does not acquire
the state row after holding an old Closed-epoch UPDATE lock. Standalone retirement
holds state and takes no lifecycle/epoch lock, so it does not introduce the reverse
order. Exact binding predicates fence each mutation statement. An activation can commit
after a statement snapshot; that cannot be advertised as permanent currentness, but
advancing an already eligible old namespace cannot shorten a live promise or touch the
new namespace. Supported activation remains offline. Keep this distinction explicit.

## Counts, contention and failure observations

Aggregate `DELETE RETURNING` receipt count and `octet_length(AcceptedPayload)` inside
PostgreSQL; never return payload bytes to the worker merely to count them. A data-modifying
CTE and a receipt EXISTS in the same statement share its snapshot: that EXISTS must not
be called post-delete emptiness. Use a separate bounded eligibility observation after
the DELETE statements and before CommitAsync, in the same READ COMMITTED transaction.
It sees this transaction's own deletes and does not introduce a post-commit query that
could hide an already confirmed result. The observation repeats the exact binding and
eligible-retirement predicates; it does not lock the lifecycle or source project.

Keep three states for eligibility evidence: unknown, eligible receipts observed, and
no eligible receipts observed. A zero count with eligible receipts visible can mean
another worker owns those rows; classify it as contention/no progress and end this
cycle rather than spinning through 64 identical zero batches. No observation promises
future emptiness. An epoch chosen by namespace/key ordering is not necessarily the
oldest eligible epoch globally; label its time as observed eligibility time, not an
oldest-backlog measurement. No exact receipt COUNT or payload scan is needed.

Publish batch count/bytes only after CommitAsync returns. A later batch cancellation,
query failure, context cleanup fault or scope cleanup fault must preserve the totals
already confirmed by prior batches. Do not infer the effect of an interrupted commit;
confirmed totals are a lower bound when its acknowledgement is unavailable. Retrying
maintenance later is safe under the same eligibility rules; no effect receipt framework
is warranted. Observation/metadata cleanup failure before commit rolls the batch back.

The cycle result should retain elapsed time, completed batches, confirmed rows/bytes,
retirement result, eligibility evidence and a typed stop reason: no eligible receipts
observed, contention, shared budget, batch cap, failure, or host cancellation. Shutdown
is not unhealthy maintenance. A timeout must not be normalized to an empty successful
cycle; distinguish shared-token expiry from an unrelated exception. Use one cycle token
for activation capture, scope, rotation, retirement, purge and observation; do not reset
the two-second budget per phase. No uncancelled diagnostic SQL follows budget expiry.

## Observable pressure without an invented capacity guarantee

Record constant-name counters/histograms for confirmed rows/bytes, cycle duration,
stop reason and observed eligible work. Keep labels to finite outcomes/phases. Profile,
namespace and operation identifiers do not belong in metric labels; a bounded warning
state can retain only the current runtime profile/namespace and reset on a change.

Warn on an unexpected failure immediately using failure type and safely extracted
SQLSTATE, with profile identity where useful. The existing raw exception logging in
the worker should be narrowed in this edit: driver diagnostics can include SQL or
private values. No exception message, parameter value, accepted payload or connection
string is needed. Preserve normal operational failure classification in the typed result.

Warn once when eligible receipts persist with no confirmed progress for
`MaintenanceWarningAfter`, or when eligible receipts plus repeated budget/batch-cap
stops persist that long. The latter is **capacity pressure**, not proof that backlog is
growing. A boolean EXISTS and deletion count cannot prove net backlog reduction; remove
that claim from the amendment. Emit a recovery transition after observed relief, and
do not repeat an identical warning every tick. A cycle deleting some rows is not by
itself recovery from persistent cap-limited work. Unknown observations must not clear
an existing pressure condition.

This supplies visible failure and an operational response, not automatic disk-pressure
admission control. A supported deployment still needs an arrival/size/storage envelope,
alerts connected to these observations, and an explicit operator action to reduce/pause
new traffic or provision capacity. Accepted promises must not be shortened to clear a
backlog. No unsupported unlimited-load or unattended disk-safety claim is acceptable.

## Focused proof for this checkpoint

Reuse the existing PostgreSQL lease, canonical migration, fixture and interceptor seams.
Retain the existing 12 epoch cases; adapt the current contiguous-retirement test to the
new sole advancement method and keep its two-horizon/tighter-options assertions. Keep
existing row/byte bounds, rotation rollback, inactive namespace and old-snapshot cases.

Proposed new Integration rows: **10**, grouped into eight declarations:

| Test | Rows | Decisive observation |
| --- | ---: | --- |
| Open current epoch with eligible history and a held current-epoch SHARE | 1 | Advances at most 128, never retires Open, returns without waiting for live writer; retained bytes remain and coherent observation refuses a retired address. |
| Locked state row | 1 | Explicit Busy with no state/epoch/evidence change; succeeds after release. |
| Rotation and retirement rendezvous, retirement-first / rotation-first | 2 | State-before-epoch order, monotonic watermark, one successor Open epoch, no stale state overwrite; use barriers, not timing assumptions. |
| Binding switches after precheck, retirement / purge | 2 | Mutation statement's exact binding fence rejects/refuses stale work and preserves all new-namespace evidence. |
| Failure after receipt DELETE before metadata cleanup/commit | 1 | Whole batch rolls back; separately committed watermark remains retired. |
| Two workers with eligible receipt locks held | 1 | No double deletion/early epoch removal; zero skipped rows reports eligible contention, then progress after release. |
| First batch committed, later shared-budget cancellation | 1 | Prior confirmed count/bytes retained, no extra batch starts after budget, incomplete work remains. |
| Maintenance query failure with sensitive diagnostic text | 1 | Failed result/Warning rather than Empty, sanitized diagnostics, prior confirmed totals retained. |

The race fixtures may seed tiny historical metadata and bounded synthetic bytes; no
wall-clock days, additional server/container, large payload corpus or production data
are necessary. Missing/Prepared transfer targets and transfer-first writer admission
are useful transfer coverage but are not dependencies of this maintenance-only slice.

Proposed Unit rows: **12**. Eight option rows cover interval below/exact lower bound,
exact/above upper bound, warning below/exact twice interval and exact/above one hour.
Four health-transition cases cover persistent no progress, persistent capped progress,
unknown observation preserving warning, and profile change/recovery resetting bounded
state. Test behavior through the actual small owner result/health logic; do not introduce
a fake epoch service interface solely for these tests. Use a clock parameter or existing
TimeProvider seam for elapsed health behavior, without waiting minutes.

That is an expected 22 new rows, plus the retained 12 epoch cases and the unchanged two
opt-in performance Facts. Parent must confirm actual discovery after implementation;
these are a proposed matrix, not discovered or passed tests. A shared concrete cycle
seam may consolidate assertions, but must not reduce the race/failure claims.

For capacity selection, rerun the finite harness for correctness/cost comparison, then
compare 60 seconds and 10 seconds with the same protected foreground workload and
unexhausted bounded retained backlog. Report accepted arrival and receipt-size mix,
foreground p95/p99, confirmed purge rows/bytes, observed backlog pressure, SQL/WAL,
allocation and relation/index/TOAST sizes. Include two maintenance workers and a
separate vacuum/reuse phase before claiming those properties. Until that evidence,
the configurable mechanism is approved; a faster default and sustainable operating
envelope remain unproven.
