# Retention capacity and maintenance amendment

Status: the narrow maintenance candidate is implemented in the isolated continuation
worktree and source-reviewed; focused execution remains pending. See the
[implementation review and exact test expectations](../reviews/P02b-retention-implementation-review.md)
and the approving [retention review](../reviews/P02b-retention-capacity-review.md).
The [complete protected baseline and matched canonical-length
comparison](../validation/P02bc-protected-performance-baseline.md) are now recorded;
neither establishes sustained capacity. The [foundation readiness review](../reviews/P02b-foundation-checkpoint-readiness.md)
keeps capacity, latency, runtime coverage and cutover gates open. This amendment does not
change issued recovery promises or select a new production cadence.

## Frozen foundation evidence and bottlenecks

The following describes the measured foundation before the maintenance candidate.
The candidate retains the one-minute default and adds the bounded scheduling,
standalone retirement and observability described below. It has no measured capacity
result yet.

`ProjectDefinitionOperationOptions` currently fixes one cycle/minute, a two-second cycle
budget, at most 64 batches, 128 receipt candidates and 16 MiB accepted payload/batch.
`ProjectDefinitionOperationMaintenance` stops at zero deletions or its budget and logs
failures, but exposes no explicit backlog/health observation. `ProjectOperationEpochService`
uses the primary-key namespace/epoch range and SQL clock, retaining the exact active
binding predicate on each purge statement.

The structural small-receipt ceiling is 8,192 rows/minute, or 136.53/s, before query
latency and contention. The complete protected baseline accepted every disjoint attempt:
146.04–219.31/s at eight writers and 140.28–183.71/s at 32. The matched canonical-length
candidate recorded 174.34–187.19/s and 140.35–144.39/s respectively. These finite
closed-loop rates exceed the small-row cleanup ceiling, but are not sustained-arrival
or production-capacity measurements. Receipt sizes and maintenance bounds are unchanged.

The original P02a comparison remains historical evidence: its 159.75 disjoint writes/s
would offer 9,585 receipts/minute if sustained, exceeding the same ceiling by 1,393/minute.
It is not the protected-protocol result and is not used to select a current cadence.
At 2 MiB/receipt, the byte bound permits only eight rows/batch, 512/minute (8.53/s).
Increasing the number of web hosts is not a safe capacity plan: concurrent cleanup
workers may compete on the same eligible epoch and skip each other's locked rows.

Default retention is not seven days of physical storage. Admission is open for one day;
the promised horizon is its close deadline plus 30 seconds plus seven days. Actual
closure can extend it. Since the current watermark advances only during another epoch
rotation, day-eight retirement can miss the extra 30-second horizon and wait until
day nine. Under otherwise regular daily operation, retain roughly **8–9 days of
arrivals**, with additional backlog, delayed maintenance and stopped-host time on top.
Issued promises remain correct; the disk multiplier is materially larger than a simple
seven-day estimate. At a hypothetical sustained 159.75/s, eight/nine days contain about
110.4/124.2 million receipts, before accounting for indexes, TOAST, page/free space or
vacuum. Full definition acknowledgements make the byte distribution essential.

The initial review identified duplicate canonical encoding in the protected large-definition
path. The measured candidate now retains the first encoding's byte length and enforces
the current canonical-byte limit after a replay miss without that second encoding/hash.
Retained replay still uses the original supported-version bounds. The matched comparison
reduced 1,000-child allocation by 1.016 MB/attempt (7.61%); latency was mixed, and significant
allocation/GC remains. This completed narrow optimization changes neither receipt size
nor cleanup capacity. It does not explain the earlier P02a tail, which predated this
protocol, or establish general latency improvement.

## Minimal cadence proposal

Make `MaintenanceInterval` a validated `TimeSpan` option in the existing Projects
definition-operation options. Proposed bounds are five seconds through one minute:
the five-second floor keeps a two-second cycle below half of one host's scheduling
budget; the one-minute ceiling avoids silently increasing the current maintenance lag.
Keep the two-second work budget, 64-batch cap, 128-row cap and 16-MiB cap unchanged.
Use startup-validated `IOptions`, not dynamically changing promises or per-write
configuration reads. This is owner-local scheduling, not a generic job framework.

Evaluate **ten seconds** as the first candidate interval after the frozen one-minute
measurement. It offers a structural small-row ceiling of 819.2/s and at most 20% cycle
duty per host, without making any transaction larger. Those bounds justify a candidate,
not a selected production default: measured deletion rate, foreground interference and
expected arrivals decide whether ten seconds is acceptable. Keep the one-minute default
until that comparison passes. Do not extrapolate a single-host capacity across hosts.
If the budget routinely expires, no interval alone is accepted without the mixed-load
proof below. Configurations combining a seconds-long admission window with year-long
retention also need a metadata-size estimate; they can retain millions of epoch rows
despite bounded per-cycle work.

Add `MaintenanceWarningAfter : TimeSpan`, proposed default five minutes, validated
between twice the configured interval and one hour. It controls warning escalation,
not receipt expiry or admission authorization. Maintain bounded in-process health
counters and a typed cycle result: completed batches, confirmed deleted receipts/bytes,
elapsed time, stop reason (`Empty`, `Budget`, `BatchLimit`, `Failed`), eligible backlog
present and time since the last successful progress. A zero deleted count alone is not
proof of no backlog: all candidates in the chosen epoch may have been SKIP LOCKED.

Use `System.Diagnostics.Metrics` as in existing `RuntimeClaimMetrics`, with constant
instrument names and low-cardinality outcome tags. Do not label metrics with operation
IDs, caller IDs or project IDs. ILogger messages may include explicit profile identity
and SQLSTATE/error type, never credentials or receipt payloads. Emit a Warning when
eligible backlog has made no progress for the configured interval, when repeated
budget-limited cycles cannot reduce it, or when maintenance fails; emit recovery after
progress resumes and avoid repeating unchanged warnings every tick. Do not swallow an
error as a successful empty cycle. A bounded receipt-index EXISTS and bounded epoch
metadata observation suffice for the worker; an exact full receipt COUNT belongs in an
explicit diagnostics query, not every tick or mutation.

## Remove avoidable rotation-only lag without a write-path guard

Recommended next maintenance refinement: advance the contiguous retirement watermark
in a separate, bounded **Read Committed metadata transaction** during maintenance,
even while the current admission epoch remains open. This needs no new per-write lock,
no receipt scan, no shared lifecycle row guard and no global advisory lock.

The existing Projects state row is not locked by ordinary definition admission; it is
read by coherent metadata/receipt queries. The refinement should:

1. Read the exact active dataset binding and server time coherently before attempting
   metadata locking. Missing/mismatched activation fails explicitly.
2. Acquire only the current namespace's Projects state row `FOR UPDATE SKIP LOCKED`.
   Return an explicit busy/no-progress result when another maintenance transaction owns
   it. Repeat exact active-binding validation in the update statement; a precheck never
   grants future authority.
3. Read at most 128 epochs beginning at `RetiredThroughEpoch + 1`; advance only a
   contiguous Closed prefix whose published horizon and actual-close-plus-retention
   have both elapsed according to the database clock. Never include the current Open
   epoch and never delete receipt rows in this transaction.
4. Commit the monotonic watermark before a later bounded purge. A crash between them
   retains excess bytes but cannot resurrect an old operation. Coherent lookups continue
   returning Expired for retired addresses, irrespective of physical receipt presence.

**Required companion change:** rotation must take the same state row lock *before*
locking its current epoch. Current rotation reads state without locking it and could
otherwise save a stale lower `RetiredThroughEpoch` after the independent advancement.
Use one maintenance order: lifecycle SHARE only when closing an expired current epoch,
then state UPDATE, then current epoch UPDATE. Standalone watermark advancement needs
only the state row and plain reads of already Closed epochs. It never waits on current
writer epoch SHARE locks. Plain coherent reads are not blocked by this metadata row
lock. Keep all maintenance transactions finite and bounded; preserve the no-guard
open-epoch precheck on the closure path.

This removes up to one admission-window cadence of avoidable retention without
shortening any promised horizon. With regular default operation, physical eligibility
then approaches seven-to-eight days plus mutation/closure/cycle delay rather than
eight-to-nine; backlog still adds unbounded extra retention unless capacity is adequate.
It must be proven against concurrent rotation, purge and activation before selection.

## Predictable behavior when capacity is insufficient

Do not add a purported global receipt/admission cap based on process-local counts or a
periodic cached COUNT. It would overshoot across hosts and could report false safety.
An exact per-write byte reservation would add a contended global counter to the owner
transaction and is outside the minimal maintenance correction.

For this slice, the release contract is an explicit supported arrival/payload/storage
envelope plus visible maintenance health and operational escalation. Existing accepted
receipts keep their full promises even under backlog. Maintenance failure retains
evidence and reports failure. Operators must reduce/pause new write traffic or increase
capacity before available disk is exhausted; no automatic deletion, retention reduction,
namespace rotation or replay-to-new-operation conversion is permitted. Database write
failures keep the writer's current boundary: known precommit failure rejects that
attempt; an unconfirmed commit returns Unconfirmed and retains its address for recovery.
Existing replay/lookup remains available whenever the database can serve it.

There is no existing supported automatic disk-pressure admission gate in this code.
Do not imply one. If unattended hard storage protection is a deployment requirement,
design a separate exact capacity-admission protocol and typed unavailable outcome
before advertising it; changing a static interval is not that feature. A release cannot
claim unlimited arrival capacity or pass this gate solely because cleanup is bounded.

## Measurements and decisive missing tests

The original partial run reported 8,192 small receipts in 823 ms and 192 large receipts
in 42 ms. Keep it separate from the completed baseline: **8,192 in 974.965 ms** and
**192 in 52.825 ms**. The matched candidate recorded **8,192 in 725.160 ms** and
**192 in 50.038 ms**. Cleanup does not use the optimized digest path, so those timing
differences are not an optimization claim. All are synthetic finite-backlog observations.
The small case stops at the 64 × 128 row cap with work remaining; the large case exhausts
its population and does not establish a service rate. None includes continuous arrivals,
foreground interference, multi-host workers or vacuum.
Do not divide either elapsed time into a production throughput promise. At the current
one-minute cadence the small cycle still admits no more than 8,192 deletions per minute,
regardless of its sub-second execution time.

### Concrete next implementation slice

Keep the two-second shared cycle budget, 64-batch cap, 128-row and 16-MiB per-batch
bounds. Replace the static interval with an instance `MaintenanceInterval` option;
name its retained one-minute default `DefaultMaintenanceInterval`. Validate five
seconds through one minute. A ten-second setting is the first matched experiment;
retain the default until foreground/tail/capacity evidence passes. Add the bounded
`MaintenanceWarningAfter` option described above. These options alter worker scheduling
and escalation only; persisted epoch admission/replay promises do not change.

Implement the coherent retirement refinement before relying on the faster cadence:
`AdvanceRetirementAsync(binding, cancellationToken)` is an owner method performing
the bounded metadata transaction above. Rotation must lock state before epoch in the
same change. A worker cycle captures one current profile/binding, attempts rotation,
advances retirement, and performs at most 64 purge calls under one linked two-second
token. No writer path gains a lock, query, counter or background task. No per-save
filesystem access is added.

Return a small typed purge observation containing confirmed deleted row/byte counts
and bounded eligible-work evidence. Count deleted bytes in PostgreSQL with a bounded
DELETE RETURNING/aggregate; do not send payloads to the worker to count them. Preserve
the exact active-binding and retirement predicates in every delete. A separate
bounded eligibility observation distinguishes an empty snapshot from work skipped due
to contention; neither asserts future emptiness. Report the observed eligible epoch's
eligibility time if available, not an invented oldest-receipt timestamp. Do not add
an exact full receipt count or a process-local admission quota.

The existing background service remains the sole scheduling owner. Factor its actual
single-cycle orchestration only if needed to share it with the controlled harness;
do not add a generic maintenance scheduler. Publish constant-name Metrics instruments
for confirmed rows/bytes, cycle duration and typed stop reason, plus current eligible
backlog presence/observed age and time since successful progress. Keep warning state
bounded to the current runtime profile/namespace, reset it explicitly on switch, and
emit one unhealthy transition plus recovery. Unexpected failures remain Warning events;
normal budget completion must not be logged as success or empty work. The external
alerting rule uses sustained eligible backlog/no progress and budget/batch-limit rates,
not a single exhausted fixture sample.

New tests must prove: option bounds; open-epoch contiguous advancement; a held earlier
long promise blocks later retirement; concurrent rotation cannot overwrite a higher
watermark or duplicate the next epoch; a crash after watermark commit leaves expired
addresses refused before physical purge; busy rows do not report an empty backlog;
server-clock eligibility and issued promise preservation; confirmed count/bytes survive
a later cycle cancellation; mismatch/failed maintenance remains observable. Run the
same finite-backlog measurements after this change, then the foreground-arrival and
vacuum comparison below before selecting a new deployment default.

The frozen two-case baseline and canonical-length comparison are complete. Repeat the
same versioned workload after the maintenance change. For capacity, use only cycles with unexhausted
backlog to estimate a service rate; exhausted cells provide a lower-bound observation.
Then compare the proposed ten-second cadence against one minute using actual protected
foreground writes plus retained cleanup, with identical child-size mix, receipt bytes,
database resources and process count. Measure foreground p95/p99/accepted rate, purge
rows/bytes per configured minute, SQL/CPU/WAL and allocation, backlog trend and index/
TOAST growth. Include one two-host maintenance cell before claiming multi-host behavior.
Capture a vacuum/reuse phase separately: current DELETE measurements do not establish
OS space reclamation or steady-state table/index size.

Current tests cover exact lifecycle binding refusal, generic/import wait-before-snapshot,
newly committed project+receipt visibility, full import refusal, lock-timeout cleanup,
retained receipt-only residue, row/byte purge bounds, nonretired preservation, both
retention horizons, rotation rollback, inactive cleanup and old Serializable snapshots.
The following focused cases are still valuable before final maintenance closure:

- Two purge workers rendezvous on one eligible epoch: no double delete, no early epoch
  removal and no false assertion that a SKIP LOCKED zero means an empty backlog.
- Inject failure after receipt DELETE but before epoch/state cleanup; verify the whole
  purge batch rolls back while its previously committed retirement watermark stays
  retired and lookup cannot execute an old address again.
- Change lifecycle binding after the purge precheck but before its DELETE statement;
  exact statement predicates must prevent the stale worker from deleting under a new
  active binding. This is distinct from the existing stale-binding-at-entry test.
- Metadata advancement and rotation overlap: the watermark never decreases, no Open
  epoch retires and candidate prefix bounds hold. Hold the current epoch SHARE while
  advancing an older eligible prefix; advancement must not wait for the live writer.
- An actual missing/Prepared target activation refuses generic and exclusive transfer
  before owner callbacks, while an Active empty target imports successfully. Existing
  mismatch tests cover a changed SQL binding, not absent activation files.
- A protected writer arriving after transfer lifecycle exclusion waits before acquiring
  owner mutation locks, then proceeds/rejects against post-transfer state; current race
  tests cover the opposite writer-first direction. Include Search and receipts in the
  transfer lock set so the original owner/table inversion remains exercised.

The original amendment was an analysis-only review. Its continuation candidate adds
source and focused tests as recorded in the implementation review; no build, test,
benchmark, database access or static scan was run by that implementation lane.
