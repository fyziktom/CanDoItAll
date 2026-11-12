# Sustained receipt maintenance validation

Status: approved harness extension integrated and built; its two accounting Facts
passed. Sustained measurement remains **unrun**. Parent owns discovery, fixture access and runs. The source preparation
and remaining proof gates are recorded in the [source note](../validation/P02b-maintenance-capacity-source.md).
This extends the [protected performance card](P02b-performance-capacity.md) and
[retention amendment](P02b-retention-capacity-amendment.md). It does not select a
production cadence or close the retained-data preflight. The historical amendment's
ten-second experiment is superseded for this next comparison by the parent's explicit
**60-second default versus five-second candidate**; the production default stays 60s.

## Why the existing lane is insufficient

`ProjectsProtectedMutationPerformanceTests` supplies two opt-in Facts and the
`protected-projects-v1` comparison. Preserve both, their workload version and results.
They use finite write waves, then at most three immediate cleanup cycles, with a
64-MiB synthetic payload cap. They do not start the registered BackgroundService.
`ProjectsOperationPerformanceProbe` retains individual EF commands in an unbounded
queue until reset; it is unsuitable for a sustained window without a bounded observer.
Its storage observation includes total relation bytes but does not break out TOAST
or observe vacuum. Do not relabel it as a scheduled, multi-host measurement.

The frozen baseline deleted 8,192 small receipts in 974.965ms and exhausted 192 large
receipts in 52.825ms. The canonical-length candidate recorded 725.160ms and 50.038ms
respectively. These precede the retention candidate and contain no sustained arrival
measurement. The exhausted large cell does not establish maximum capacity. At the
unchanged per-cycle caps, one host can delete at most 8,192 small receipts per cycle,
before time, payload and contention limits; scheduling controls how often it may do
so. See the [retained matched evidence](../validation/P02bc-protected-performance-baseline.md).

## Prepared minimal source extension

No production changes or new maintenance API are required. The prepared files below
are relative to the managed repository.

| File | Responsibility |
| --- | --- |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectsMaintenanceCapacityTests.cs` | Four inline theory rows, fixture ownership, phases and correctness assertions |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectsMaintenanceCapacityLoad.cs` | Fixed open-loop schedule, bounded queues, immutable outcome/timing aggregation and bounded EF command-family observations |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectsMaintenanceCapacityStorage.cs` | Allowlisted exact phase counts, physical relation/statistics/WAL observations and explicit quiescent vacuum |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectsMaintenanceCapacityWorker.cs` | Owned child launch/cleanup, sequence accounting and post-exit backend evidence |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectsMaintenanceCapacityResources.cs` | Immutable pre-allocation ownership manifests, exact random database namespace, contained environment roots and ordered cleanup evidence |
| `tests/Integration/CanDoItAll.Tests.Integration/ProjectsMaintenanceCapacityAccountingTests.cs` | Two pure accounting Facts described below; no database or child process |
| `tests/Support/CanDoItAll.ProjectsMaintenance.TestHost/CanDoItAll.ProjectsMaintenance.TestHost.csproj`, `Program.cs` and `MaintenanceHostProtocol.cs` | Bounded child transport, connection evidence and actual registered maintenance worker lifecycle; protocol source linked into Integration |
| Existing Integration `.csproj` | Build-only child-host reference with `ReferenceOutputAssembly=false`; standard `Debug`/`Release` output; no solution membership change |

Use the existing Support/Projects dependencies. Do not reference Integration from the
child, add a production friend assembly, copy the cycle algorithm, or generalize the
historical probe into a new diagnostics framework. Keep the new helpers internal and
specific to this workload. Existing performance files need no behavioral changes.

Prepared runner method:
`Measure_sustained_arrivals_with_scheduled_maintenance_and_storage_reuse`, with exactly
four `[InlineData]` rows `(60, 1)`, `(60, 2)`, `(5, 1)`, `(5, 2)` for cadence seconds
and maintenance process count. Apply `Category=LongRunning` and `Category=LiveProcess`
to this method, plus the repository's applicable host-platform trait. Both payload
profiles below execute sequentially within each row on fresh database leases. This
means **four opt-in discovered/executed rows, eight physical cells per repetition**.

The two ordinary accounting Facts are
`Worker_confirmation_streams_do_not_double_count_or_convert_missing_evidence_to_empty`
and `Offered_arrivals_account_for_queue_rejection_and_unconfirmed_completion`.
They exercise stream sequence/reset/lost-confirmation handling and the offered-work
identity partition; they do not mirror record properties. The first also rejects
malformed, misplaced, incomplete and out-of-order transport evidence. Source-derived
additions are **six rows: four explicit inline rows plus two Facts**. The child has no
test SDK and does not change Broad Stable's 36-assembly membership.

## Fixed workload and bounded resources

Emit `WorkloadVersion=protected-projects-maintenance-v2`. Use the current protected
writer, coherent reader, enlisted Search, original expectation, receipt and lifecycle
guards. Caller remains the historical performance lane's synthetic LocalWorkspace
with explicit allowed read access. Do not imply HTTP or governed-source costs.

| Profile | Definitions / expiry payload sequence | Offered foreground and synthetic expiry arrivals | Measured arrivals in 360s |
| --- | --- | --- | --- |
| Small | Five phases and five options in every operation | 150 receipts/s; payload rate `150 * Bsmall` bytes/s | 54,000 |
| Mixed | Repeating 20-operation sequence: 19 Small, one 1,000-phase/1,000-option Large | 50 receipts/s; average payload rate `(47.5 * Bsmall) + (2.5 * Blarge)` bytes/s | 18,000 |

`Bsmall`/`Blarge` are the exact encoded receipt lengths of the fixed synthetic pool,
recorded before the run. Use fixed-width synthetic labels so updates do not grow the
payload. Assert measured real lengths against the declared pool's bounded range and
report their actual sum separately. Historical sizes 2,171/291,319 bytes imply about
325,650 bytes/s Small and 831,420 bytes/s Mixed; these are examples, not substituted
measurements. Per-second Mixed buckets alternate the exact 19:1 sequence across bucket
boundaries, so both the 360s row totals and byte totals are deterministic.

Eight foreground executors each own disjoint Small and Large project targets, so the
measurement does not intentionally create definition conflicts. Capture context once
per executor opening; read the original definition for each newly admitted logical
attempt. Never refresh its basis or operation identity for a retry. There are no
automatic retries. Use an open-loop Stopwatch schedule and a total queue bound of
256 requests. A full queue records a rejected offered arrival; it must not slow the
offered clock or silently discard work. Report scheduled-to-completed latency,
queue delay and owner-call latency separately. Service-only successful percentiles
must not conceal queue drops, overdue requests or unconfirmed outcomes.

Preflight each cell against **4GiB raw accepted-payload bytes**, including seeded
history and the worst-case foreground/control/reuse schedule, and **250,000 total
receipt rows**. Parent also checks sufficient free space for heap, indexes, TOAST,
WAL and temporary headroom on the owned fixture; 4GiB is not a physical-disk bound.
Record the arithmetic and abort before seeding if it cannot fit. Do not shrink a
failed workload silently. Retain at most one physical cell at a time.

The harness requires the parent-recorded environment value
`CANDOITALL_TESTS_MAINTENANCE_CAPACITY_APPROVED_BYTES` to be at least **17GiB** before
seeding. That is four times the raw ceiling plus one GiB of additional headroom, not
a measurement or automatic server-disk discovery. Parent must record current physical
capacity for the exact owned fixture; an ordinary application host's disk is not evidence.

The exact seed bound is 119,537 Small receipts, or 83,537 Mixed receipts consisting
of 79,361 Small and 4,176 Large. At the retained example lengths these are
259,514,827 bytes (247.49MiB) and 1,388,840,875 bytes (1.294GiB), respectively.
Recalculate from the actual canonical accepted payloads before any insert; labels
or child counts alone are not a byte-size proof. The larger initial cohort is needed
only to exceed three full two-worker cycle groups without exhaustion. Neither cell
is retained when the next starts, and no maximum-size receipt expansion is proposed.
Parent preflight on 2026-10-16 recorded 988,024,926,208 available bytes on the owned
PostgreSQL volume and 524,272,721,920 bytes on its backing Windows drive, exceeding
the 18,253,611,008-byte headroom requirement. The owned mount is `/var/lib/postgresql`.
This is capacity availability, not a measurement result. Recheck before seeding;
each cell must still validate its actual byte/row arithmetic.

Per cell phase limits are initialization 180s and seeding/analyze 180s separately;
warmup offers for 30s within a 60s settle bound; foreground-only control offers for
120s within 150s; measured arrivals offer for 360s within 390s; scheduled drain lasts
120s; pressure/Busy drills share 480s; quiescent vacuum/reuse shares 300s. Child launch
is bounded by 60s, normal stop by 15s, and forced owned-process exit and pipe completion
by separate ten-second bounds. The enclosing cell deadline is 30 minutes, including
normal child shutdown, and dominates the sum of per-phase maxima. Bounded cleanup
still runs after cancellation. A slow phase records a failed/incomplete observation
instead of extending its deadline. Each theory row has two cells and a 65-minute outer deadline;
the parent chooses an appropriate serialized runner deadline for four rows. These
are bounds, not expected durations. No extra Docker stack is created per cell.

These in-process limits are cancellation deadlines. Existing shared
`TestApplication.CreateAsync` and `DisposeAsync` do not accept cancellation, so fixture
bootstrap/disposal cannot be forcibly interrupted by this cell token. Parent must
enforce a process-level runner deadline and retain the resource ledger for forced
lease cleanup after an interrupted runner. Do not detach a timed-out fixture task
with `WaitAsync` and allow it to continue creating resources unobserved. The 180s
initialization phase begins after shared fixture bootstrap and covers actual template
creation; the card does not claim a hard wall-clock bound for shared fixture code.

## Expiry arrivals without changing real promises

Prepare a fixed pool of actual accepted Small/Large payloads. Clone only these opaque
bytes into an **inactive synthetic namespace** with owned state/epoch/receipt rows,
as the existing finite-backlog fixture does. These synthetic operation addresses are
not valid replay evidence: their embedded payload address intentionally differs from
the synthetic row address. They are never decoded or submitted to lookup or the writer.
Only the separately retained active real templates prove exact replay. The real
active namespace retains the normal one-day admission and seven-day replay settings.
No accepted real epoch is edited, reactivated, shortened or retired for timing.

Seed 65,537 initially eligible rows, followed by 360 one-second expiry buckets holding
the row/byte arrival sequence above. All clone inserts finish before timing. Choose
the server-clock anchor after preparation, while workers remain stopped. One coherent
fixture metadata update establishes the schedule before taking the baseline snapshot.
For a bucket eligible at `S + n seconds`, use an already closed inactive epoch with
`ClosedAtUtc=S-2 days`, admission close one second earlier, mutation deadline 1,000ms,
and replay retention `172800+n` seconds. Both issued and actual-close horizons then
expire at the intended time. Initial backlog has both horizons safely before `S`.
The values satisfy the existing persisted epoch constraints; do not bypass them.
These 360 short synthetic epoch buckets add metadata work unlike the ordinary daily
epoch cadence. Report that overhead with the cohort result; do not generalize its
retirement rate to the production epoch distribution.

This models expiry arrivals by the SQL clock, not additional clone INSERT traffic
during measurement. Real protected writes concurrently replenish retained storage.
The synthetic expiry rate is fixed even if foreground acceptance falls below offer;
report both independently. This is a controlled old-cohort/new-cohort experiment,
not a claim that newly accepted receipts expire within minutes.

65,537 initial receipts exceed three complete pairs of the structural maximum
two-worker 8,192-row cycles. Require at least three full cycle groups with demonstrably
unexhausted eligible backlog for a service-rate estimate. Validate this using the
prepared eligibility schedule, worker confirmation streams and exact phase-boundary
counts. An earlier finish, lost confirmation or insufficient backlog makes that
estimate unavailable. Later empty cycles demonstrate relief/headroom only. Include
the immediate first worker cycle in totals, but do not turn its startup burst into a
per-minute capacity claim; compare complete elapsed scheduling windows.

## Actual worker processes and transport

The child uses existing `TestApplicationBootstrap.ConfigureDefaultServices`, builds
its provider with validation, and resolves the explicitly supplied owned profile and
active binding. The parent alone initializes/migrates the lease. The child does not
call `BuildServiceProviderAsync`/`InitializeSchemaAsync`, start a web server or start
all registered hosted services. Select exactly the registered
`ProjectDefinitionOperationMaintenance` from `IHostedService`, then call its actual
`StartAsync` and `StopAsync`. Its existing PeriodicTimer, initial immediate cycle,
two-second budget, 64 batches and row/byte bounds remain unchanged.

Use a small version-one JSON-lines control protocol on redirected stdin/stdout.
Typed messages: Ready, Start, CycleObservation, HealthTransition, Failure, Stop and Stopped;
every frame carries run nonce, worker ordinal and strictly increasing sequence.
Arguments contain only profile ID, run nonce, ordinal, cadence, warning interval and
maximum lifetime. Credentials and control-plane/storage paths are passed through
`ProcessStartInfo.Environment` using the owned TestDatabaseProfile environment values,
never arguments or logs. Scrub inherited database/control-plane/storage overrides
as in `DatasetActivationCommandProcessTests`. Reject missing or mismatched binding.

Attach a MeterListener only to `CanDoItAll.Projects.OperationMaintenance`, with an
explicit instrument/tag allowlist. Record cumulative confirmed count/bytes and typed
cycle stop/eligibility, duration and available age/progress observations. The current
metrics emit count, bytes, cycle and duration followed by optional eligibility/age/
progress observations from one worker. Close the previous cycle at the next count,
or explicitly flush after worker shutdown, so optional observations remain attached
to their actual cycle. This delays delivery by up to one cadence. Required zero-valued
count/byte observations must actually arrive; absence is invalid evidence, not zero.
Do not fabricate private retirement
state or exact backlog from absent instruments. Capture only the maintenance logger's
known sanitized failure/health fields; arbitrary startup diagnostics are not emitted.
Allow 4KiB/frame, 2,048 frames/child and a bounded nonblocking output queue. Overflow,
unknown frame, sequence loss, invalid enum or child failure invalidates measurement;
never block the worker on slow pipe output or drop a record silently.

Two-worker rows mean two independent OS processes, providers, pools and health states
sharing the same owned database/profile. Start both from Ready at the same parent
barrier, record actual offsets, and retain per-worker streams. They are independent
hosts on one physical machine, not a distributed-machine scalability test. Do not
count the foreground measuring provider as a third maintenance host.

Record planned executable, profile/database lease, root and run nonce in bundle
resources before launch; append exact PID/start time once started. On every path:
send Stop, bound graceful shutdown, kill only the owned process tree if necessary,
await exit, dispose pipes and provider, then verify owned database sessions are gone
before dropping the lease. A pipe EOF also stops the child; a finite child lifetime
prevents an orphan after parent failure. Preserve primary and cleanup failures.
Do not delete unrelated roots or touch the ordinary application on port 5032.

A test-only connection interceptor records the actual Npgsql backend PIDs opened by
the child's Projects contexts, with a hard bound of **16 distinct PIDs**. Exceeding
the bound fails the stream; it never truncates evidence or changes connection/profile
identity. Only a valid Stopped observation carries this nonempty unique array. After
owned process exit, the parent polls `pg_stat_activity` in the exact leased database
for at most ten seconds and requires all recorded PIDs to be absent. This adds no SQL
inside measured cycles. Abrupt exit or missing Stopped cannot prove individual backend
cleanup: record incomplete session evidence, retain the failed result, and rely on the
existing forced database-lease cleanup without claiming per-child verification.

## Phase observations and physical accounting

Keep autovacuum enabled with identical settings in every cell. Warm up before the
control window. In the 120s foreground-only control, maintenance children have not started.
The 360s measured window starts actual workers and the fixed expiry schedule together.
After arrivals stop, observe at most 120s of real scheduled draining. Do not issue
manual immediate cycles to make the configured cadence appear sufficient.

At phase boundaries, stop new work and reconcile settled operations before exact
SQL COUNT/SUM observations. The periodic observer uses cheap statistics and the
known expiry schedule, not full receipt scans on every tick. Exact counts include
inactive synthetic and active real namespaces separately. Compare actual removed
rows/bytes against the sum of confirmed worker batches: a commit with a lost response
can remove more than the confirmed counter, but can never justify double counting.
Label unresolved accounting explicitly until a quiescent exact read settles it.
Do not derive successful deletion from a worker exit code alone.

Record, by fixed window and process where meaningful:

- Offered, enqueued, queue-rejected, started and completed identities; new acceptance,
  replay, typed rejection, Unconfirmed and sanitized SQLSTATE/failure type; queue high
  water, oldest waiting age, and all latency distributions. Every offered identity
  must belong to exactly one current outcome partition.
- Confirmed durable receipt count/bytes by worker, actual cycle timestamps/duration,
  stop and eligibility; aggregate without double counting. Report the two-second
  cancellation budget and actual completion overshoot, including cleanup. A failed
  or canceled cycle is not an empty cycle.
- Bounded EF command-family aggregates, process CPU time/allocation/GC and owned
  PostgreSQL resource samples if the parent's fixture collector supplies them.
  EF observations omit raw lifecycle SQL and materialization; process allocations
  and overlapping command duration are not per-request attribution.
- Receipt heap main/FSM/VM sizes, table bytes, index bytes and total relation bytes;
  explicitly obtain TOAST heap/index/total through `reltoastrelid`. Total already
  includes TOAST/index components: never add the breakdown to total again. Also
  capture state/epoch and Project/child/Search relation totals to distinguish costs.
- Exact raw payload count/sum and distribution at boundaries; `pg_stat_user_tables`
  live/dead estimates, modifications, vacuum/autovacuum/analyze counters and times;
  database IO/deadlock/temp counters and effective autovacuum/storage/compression
  settings. Statistics can lag; they are not an exact retained-row proof.
- Cluster insertion-LSN delta and `pg_stat_wal` deltas. These are **cluster-wide**,
  including other databases/background activity, not even database-specific and
  certainly not receipt-specific. The parent runs this lane only after the broad
  Stable attempt has ended and its disposition is reviewed, the applicable focused
  repairs/accounting/static prerequisites pass, and the fixture is otherwise quiescent: no parallel database lane,
  .NET build or SPA build runs. That restriction does not make WAL database-specific.
  Separate setup, foreground/maintenance, vacuum and refill windows. Do not reset
  cluster statistics, force a checkpoint, or assign all mixed-window WAL to deletes.

After workers and foreground are stopped, record residual eligible work before any
fixture cleanup. If the 120s drain bound leaves backlog, report it; explicit synthetic
cleanup outside measurement does not turn the scheduled lane into a capacity pass.
Perform a separately timed ordinary `VACUUM (ANALYZE)` on the owned receipt/epoch/state
tables using an autocommit connection. There is **no implicit forced vacuum during
foreground timing**. Observe any natural autovacuum that did run; if none ran, the
foreground result contains no autovacuum-interference proof.

For reuse, perform three bounded quiescent rounds, each inserting the same 8,193-row
opaque cohort with the cell's fixed payload mix, removing that exact synthetic cohort,
then ordinary vacuum. Record row/byte truth, heap/index/TOAST allocation, WAL and elapsed
time before and after each phase. This tests allocated-space reuse under the declared
shape; it is not another scheduled-capacity result. No VACUUM FULL, REINDEX, TRUNCATE
or database replacement occurs between these rounds. Ordinary vacuum primarily makes
dead space reusable; physical-file shrinkage is not the pass condition. PostgreSQL's
[vacuum guidance](https://www.postgresql.org/docs/18/routine-vacuuming.html),
[size functions](https://www.postgresql.org/docs/18/functions-admin.html) and
[statistics semantics](https://www.postgresql.org/docs/18/monitoring-stats.html)
define these accounting limits.

## Pressure, recovery and Busy observability

Run these separately from nominal arrival timing, once per cadence/process-count row,
using the Small cell. Use the same 120s warning threshold for both cadences; it meets
the validated minimum of twice 60s and is an explicit test setting, not a change to
the production five-minute default. Bounded locks belong to an identified test
connection and are always rolled back/disposed.

The parent-approved 480s pressure bound accounts for a 120s health threshold sampled
at the actual 60s cadence plus delivery of the complete cycle frame on the next cycle.
It does not add a production metric event or manufacture immediate maintenance calls.
The 30-minute cell and 65-minute row limits still apply.

First leave one eligible receipt locked while the workers run and no other eligible
work exists. Require observed Present/Contention and a sustained NoProgress warning;
release it and require confirmed deletion plus a later non-Busy Empty observation
before Recovery. One positive delete while known eligible work remains is not relief.
Existing deterministic tests retain responsibility for lost-ack/cleanup/cancel races;
this timed lane verifies actual scheduling and transition delivery, not every fault.

For the Busy-only branch, hold the active operation-state row so retirement reports
Busy while no already-retired receipt is available. Later unretired backlog is
**unknown**, not measured empty. The implementation correctly avoids Recovery and
does not create a new eligible-work warning without confirmed eligible work. Require
Contention cycle records and no invented recovery/backlog claim. Existing unhealthy
state must not be cleared merely by this observation.

This is eligible-retention health, not general admission readiness. The current
`RotateExpiredAsync` boolean does not distinguish not-due from busy. Real foreground
context opening proves availability only during this non-expiring, default-duration
epoch; it cannot prove timely rotation at an admission deadline. No overall worker
health claim follows. Such a claim additionally requires a separately timed context
availability observation around actual epoch expiry and rotation contention, retaining
the issued mutation/replay promises. That proof is outside these four capacity rows.

Low-cardinality Contention cycles are sufficient to support an external alert about
prolonged inability to advance/observe maintenance; they are not evidence of backlog
size. Use a timed rule covering at least MaintenanceWarningAfter and two scheduled
observations, and separately alert on missing cycles beyond two cadences plus budget
allowance. Instance identity belongs to exporter resources, not operation labels.
Brief contention between two healthy workers is expected. The current slice does not
install an exporter or alert rule. A deployment without such collection has an
observability gap for sustained Busy with unknown backlog; it cannot claim complete
operator warning coverage from ILogger alone. Keep that release gate explicit.

## Fair comparison and qualification

First rerun the unchanged two-Fact v1 lane on the retention candidate to retain a
bridge to the frozen evidence. Then run all four v2 rows on the same frozen candidate
binary: 60s versus 5s isolates configuration, and one versus two isolates host count.
Do not compare a new v2 five-second result directly with an old v1 immediate-cycle
result. A pre-retention source comparison, if later required, needs its own compatible
versioned scheduled harness and separately built standard outputs; it is not implied.

Use fresh leases in the same existing owned PostgreSQL fixture, identical fixed pool,
rates, server settings, resources, GC/build/runtime and warning threshold. Record
actual xUnit row order, startup offsets and all source/diff/sibling identities. Run
two independent repetitions with comparison order reversed; do not pool percentiles
or select the favorable sample. No rate, seed or acceptance threshold changes after
observing failures without a new named workload revision and retained failed record.

Correctness is mandatory in every row: no early or active receipt deletion, no
changed retained payload, exact sampled original-address replay, no duplicate effects,
complete offered/outcome accounting, and no unreported child failure or cleanup leak.
The harness reports performance qualifications separately from those assertions. A
60s control that cannot keep up is useful evidence, not permission to discard it.

Before execution, the parent reviews these proposed engineering qualifications for a
supported measured envelope; they are not existing product SLOs:

- The offered foreground rate is sustained without queue drops or a growing queue;
  no unexpected owner rejection/Unconfirmed is hidden among successful latencies.
- Across both matched repetitions, p95 stays within control `*1.20 + 5ms`, p99 within
  control `*1.25 + 10ms`, and end-to-end p99 below 1s. Report all windows/outcomes and
  absolute values even if qualification fails; the thresholds cannot be relaxed
  after measurement merely to obtain a pass.
- Eligible backlog has no increasing late-window trend at the stated arrival mix.
  A service-rate claim needs the unexhausted evidence above and measured 20% recovery
  headroom; exhausted windows provide only a lower bound. Two hosts are not presumed
  to double throughput. Report per-host CPU and foreground interference together.
- Every nominal cycle finishes or explicitly reports bounded cancellation/failure.
  Repeated budget overshoot, failed worker shutdown or unresolved durable accounting
  blocks qualification; a two-second token is not asserted to be a hard wall-clock SLA.

Two-minute control, six-minute arrivals and three reuse rounds cannot establish
long-term 8–9-day retained storage, steady-state bloat or arbitrary production mixes.
The clone pool is compressible synthetic data; high-entropy fields, 2-MiB receipts,
year-long retention/tiny epochs, governed authority, HTTP/UI, a full busy web host,
different machines and real production distributions remain outside these results.
No production read is authorized by this card. A supported deployment envelope still
needs separately authorized aggregate retained-data preflight and disk headroom.

## Runner and evidence contract

Parent confirms the current resource ledger and ignored fixture ownership metadata,
including the current loopback port, before any lease/child launch. Reuse the owned
PostgreSQL18 container; do not infer its current endpoint from a historical log.
Build standard Release serially, then source-count/discover exactly four rows using:

`FullyQualifiedName~ProjectsMaintenanceCapacityTests&Category=LongRunning&Category=LiveProcess`

The four InlineData values define four cases; their declaration order is **not**
an execution-order contract. For matched repetitions, invoke one row per process.
The current Integration project uses xUnit 2.9.3 / VSTest adapter 3.1.4;
[VSTest supports xUnit DisplayName filters](https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests).
Use this exact common prefix, then the row suffix from the table (one quoted filter
argument, not separate shell arguments):

```text
FullyQualifiedName=CanDoItAll.Tests.Integration.ProjectsMaintenanceCapacityTests.Measure_sustained_arrivals_with_scheduled_maintenance_and_storage_reuse&Category=LongRunning&Category=LiveProcess
```

| Row | Append to common prefix | Source-derived expected selected cases |
| --- | --- | ---: |
| 60s / one worker | `&DisplayName~cadence: 60&DisplayName~workerCount: 1` | 1 |
| 60s / two workers | `&DisplayName~cadence: 60&DisplayName~workerCount: 2` | 1 |
| 5s / one worker | `&DisplayName~cadence: 5&DisplayName~workerCount: 1` | 1 |
| 5s / two workers | `&DisplayName~cadence: 5&DisplayName~workerCount: 2` | 1 |

After the standard Release build, the parent runs `dotnet test` on the Integration
project with `--configuration Release --no-build --list-tests --filter '<filter>'`
for each of these four filters. Each must discover exactly one row with the stated
two argument values; also retain the four-row aggregate discovery. A changed display
format or unsupported filter is a readiness failure, never permission to run the
whole theory and infer its ordering. Those discovery checks have not run yet.
Use the identical verified filter for execution. Repetition A runs 60/1, 60/2, 5/1,
5/2; repetition B runs 5/2, 5/1, 60/2, 60/1. Record actual start/end order and all
eight row outcomes. No source, attribute, test-sorter or binary changes between them.
This remains four source cases, not eight new tests.

The parent records the selected row, exact filter, binary identity, evidence root,
runner PID/start identity and 65-minute process deadline **before** execution.
The row's cancellation token is cooperative; `TestApplication` bootstrap/disposal
is not cancellable. A process deadline therefore needs owned-tree termination and
manifest-guided cleanup after verifying identity. Preserve an interrupted log/TRX
and incomplete result; never detach bootstrap/disposal and abandon its lease.
The parent runner and the four one-row discovery checks are still pending.

### Capacity ownership amendment

Parent approved a test-only correction to the prepared harness: require an existing
absolute runner-owned `CANDOITALL_TESTS_MAINTENANCE_CAPACITY_OUTPUT_ROOT`, with immutable
CreateNew phase manifests outside disposable environments. Before `CreateUnder` or
database allocation, record cell/run/row identity, parent process identity, exact
cell-parent path and a fresh 20-character random lowercase hex profile key. The
existing lease naming rule then gives the exact prefix `cditall_<20hex>_`, followed
by exactly 32 lowercase hex characters. This does not use a predictable shared
`primary` prefix. After allocation, record the actual root and database name from
the returned profile, never its connection string. Pass the existing
`TestHarnessOptions.TestEnvironment` and `ActiveProfile`; no shared fixture API change.

The test owns provider disposal before environment disposal and records each cleanup
outcome in persistent evidence. Child prelaunch/start/stop manifests move into the
same evidence directory. Resolve paths under the pre-recorded root and reject
reparse-point escapes; refuse reused cell parents. A crash before the exact database
name is recorded permits only parent reconciliation on the separately recorded owned
fixture using the exact random prefix plus suffix rule, with at most one match.
Multiple matches or unverified fixture/process/path identity stop automatic cleanup.
Never search/drop generic `cditall_*` databases. Initial ownership intent survives a
kill during any later phase write. This amendment is source prepared, has not been
built or executed, and leaves the four-plus-two case counts unchanged.

The parent supplies two explicit inputs: `CANDOITALL_TESTS_MAINTENANCE_CAPACITY_APPROVED_BYTES`
for recorded physical headroom, and `CANDOITALL_TESTS_MAINTENANCE_CAPACITY_OUTPUT_ROOT`
for the existing runner-owned persistent evidence directory. The latter also holds
the parent's fixture identity/deadline records; the test never guesses fixture
ownership from the ordinary host. Cell manifests use `capacity-<run>-<sequence>-<phase>.json`,
CreateNew, WriteThrough and flush-to-disk. They are not overwritten/deleted by normal
environment disposal. Partial trailing manifests after a kill do not invalidate the
earlier completed CellIntent; do not infer a completed phase from an incomplete JSON
file. Child records carry exact worker run/ordinal, assembly and PID/start identity.
The first cell intent precedes environment/database allocation; the exact returned
database name is recorded before application bootstrap. Cleanup acknowledgements do
not substitute for parent verification after a killed/incomplete lease allocation.

Discover the two pure rows independently with
`FullyQualifiedName~ProjectsMaintenanceCapacityAccountingTests`. No fresh runner list
is used to invent expected counts. Child assembly resolution uses the standard
Support project's `bin/<BuildConfiguration>/net10.0` path. No custom configuration,
provider invocation, arbitrary output-path argument or secondary server is needed.

Emit bounded allowlisted JSON through xUnit output into the parent's TRX/log. Preserve
failed and incomplete rows, raw aggregate observations and a concise comparison with
scope limitations. Update the resources ledger with verified process/session/lease
cleanup; retain the shared container only while another authorized lane still needs
it. Source preparation launches nothing and makes no measured capacity claim. A green
correctness row alone does not set the emitted qualification result to true: matched
reverse-order repetition, late-backlog analysis and measured recovery headroom remain
explicit parent review gates.
