# Protected Projects performance and retained-capacity gate

Status: implemented opt-in harness; baseline and canonical-length reuse candidate
each completed all two Facts / 33 samples. The [matched comparison](../validation/P02bc-protected-performance-baseline.md)
records measured outcomes and limitations. This card retains the workload and
capacity gate design; mixed maintenance/writer capacity and production retained-data
preflight remain pending. No ordinary application database was accessed.

## Source and comparison contract

Keep `ProjectsMutationPerformanceTests.cs` and its baseline/candidate evidence intact.
Those historical measurements used legacy editor reads/saves and optional postcommit
search. They are not a protected-write measurement, and that legacy test is expected
to require its historical source to execute after compatibility writes are disabled.

`ProjectsProtectedMutationPerformanceTests` is a new `Category=LongRunning` workload,
emitting `WorkloadVersion=protected-projects-v1`. Expected discovery is **two Facts**:

1. `Measure_protected_replacements_with_matched_shapes_and_tail_diagnostics` retains
   the 24 samples, 2,400 measured attempts, 480 warmup attempts, 0/5/100/1000 children
   per collection and 1/8/32 writer shapes. Setup uses protected Create; measured
   operations use the coherent definition reader and protected full Replace. Every
   wave finishes all reads before any saves. Same-target contenders retain their
   original expectation; failures are never retried or normalized out of results.
2. `Measure_protected_create_replay_lookup_and_bounded_receipt_cleanup_capacity`
   separately reports three samples of 100 creates, 100 exact-envelope replays and
   100 receipt lookups (900 measured operations), plus 60 create warmups and bounded
   cleanup-only backlog cells. These samples are not added to the matched 2,400.

Fresh context metadata is captured once per worker at each measured sample opening,
then reused for its 100 attempts. That opening's queries, wall time and allocations
are included in `EndToEndBatchMilliseconds` and process allocation/attempt; metadata
is not repeatedly fetched per save. Save-only latency is separate. Per-attempt
end-to-end latency includes read, immutable command preparation and the wave barrier;
opening context wall time is reported separately because it serves multiple attempts.
Create samples likewise include their one opening query in batch totals. Replay and
lookup preserve the original context and perform no metadata refresh.

This is a matched *shape* comparison, not identical code or request serialization:
typed definition reads omit legacy editor placeholder options; full receipts and
required enlisted Search add work; caller policy is an explicitly allowed synthetic
LocalWorkspace boundary. HTTP authentication, browser persistence/rendering and
source-authorized Serializable authority costs are outside this lane. The complete
definition writer, original CAS, lifecycle/epoch guards, receipt storage and required
atomic Search remain enabled.

## Collection and interpretation

`ProjectsOperationPerformanceProbe` observes asynchronous EF commands only. It tags
each attempt and command family, emits counts and summed duration, and never emits SQL,
parameters, names, descriptions, notes, identities or credentials. Raw lifecycle
`DbCommand` calls bypass EF interception, so these are **observed commands**, not full
SQL statement counts. Reader completion excludes subsequent materialization; parallel
command durations overlap. The five slowest operations per sample retain only their
timings, outcome codes, observed SQL family durations and overlapping process GC counts.

Process-wide allocated bytes divided by attempts includes DI scope creation, reads,
context opening, draft preparation and instrumentation. It is an allocation estimate
per offered operation, not precise concurrent-request allocation attribution. GC
overlap is a correlation, not proof of causation. Save distributions separate new
accepted writes; outcome dictionaries retain every conflict, replay, unconfirmed result
and fault SQLSTATE/type. Correctness assertions require one accepted result per
same-project wave, all uncontended/disjoint writes accepted, and no duplicate effects
or new receipts during replay/lookup. There are no hard latency assertions.

Before/after SQL aggregates report receipt count, uncompressed accepted payload bytes,
mean/p95/max payload size, heap bytes, all receipt index bytes and total relation bytes
(including TOAST and indexes). Storage deltas at small sample sizes include page
allocation granularity. WAL uses the cluster insertion LSN difference: it includes
background WAL and is meaningful only in the parent's isolated, serialized lane.
It is not attributed by database or query. Results include bytes/attempt and, when
nonzero, bytes/new accepted write. DELETE does not imply OS space reclamation.

The 1,000-child case also runs a bounded `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` of
the synthetic phase read after timing samples. Output contains only node/index names,
estimated/actual rows, loops, durations and buffer counts. No production fields are
selected into output. This is a representative child-read plan, not the whole write's
plan or a reconstruction of private production distributions.

## Tail investigation before optimization

The P02a repeated 1,000-child p95 regression remains open. Source inspection finds:

- `ProjectDefinitionReader` materializes ordered child arrays and the immutable
  snapshot copies them; the client builds immutable replacement arrays.
- `ProjectDefinitionRules.Normalize` and `ProjectDefinitionNormalizedIntent` make
  bounded linear validation passes, while writer child matching uses dictionaries.
  This does not show a quadratic child lookup in the current path.
- `ProjectDefinitionOperationCodec.ComputeDigest` encodes full canonical intent before
  hashing. Accepted-result encoding constructs bytes, `ProjectDefinitionEncodedResult`
  takes an owned copy, and insertion obtains another byte copy. These potentially large
  allocations are candidates for attribution, not permission to weaken immutability.
- The receipt stores the full accepted child mapping and normalized result. That cost
  grows with definition size even if only three fields changed. Search still uses all
  option summaries but now shares the original owner transaction.

First compare repeated v1 samples by size: save p95, complete batch throughput,
allocation/attempt, generation-two collections, payload/WAL per acceptance and observed
database duration. Use the recorded slow attempts to distinguish database-duration
growth from time outside observed commands. If the tail persists and these observations
do not explain it, collect one bounded .NET allocation/GC trace of this owned process
with the same workload before changing production allocation ownership. Do not infer
lock wait from advisory duration, or GC causality from overlapping counts. A repeat
run is required when a single tail sample is the only evidence of a material change.

## Maintenance and capacity model

The real options currently cap a cycle at two seconds, 64 batches, 128 candidates per
batch and 16 MiB accepted-payload bytes per batch, with one scheduled cycle per minute.
The structural row ceiling is therefore at most 8,192 rows/minute (136.5/s) before
SQL time, payload size, contention, epoch cleanup and backlog distribution. It is an
upper bound, not sustainable throughput. A full-size 2 MiB receipt reduces the byte
ceiling to eight receipts/batch, or 512/cycle before other costs. Existing requests
can be much smaller; measure actual distributions before selecting operating limits.

The harness explicitly removes only the receipt maintenance hosted-service registration
from its owned measuring application; TestApplication builds a service provider rather
than starting ordinary hosted services. It then runs the actual epoch service with the
same bounded cycle algorithm, budget and maximum batches. Foreground operations retain
all production guards. Two cleanup-only inactive namespaces contain cloned payloads
from actual accepted 5-child/1,000-child results: up to 8,193 small or 192 large rows,
each cell capped at 64 MiB of synthetic payload. These are intentionally not replayable
operation addresses and are never submitted to the writer. They model opaque retained
cleanup cost without thousands of unrelated setup writes. Only the leased test database
contains these rows. There is no synthetic activation or namespace change in production.
After recording a cell, exact-namespace fixture cleanup removes any remaining synthetic
rows before the next cell, so one cell's backlog cannot contaminate the other's counts.

Each cell runs at most three immediate bounded cycles, reports confirmed batch counts
and independent before/after receipt counts, and verifies row/byte bounds and preservation
of active receipts. Independent counts account for a commit whose response is interrupted
by the cycle budget. An exhausted backlog cannot establish maximum capacity. Immediate
test cycles are not a claim that production schedules cycles faster than once/minute.

Reports compare each observed deletion count per configured minute to explicit examples
of 1, 10 and 100 accepted operations/s. These are assumptions, not observed production
arrival rates. Stable operation requires sustainable purge rate above expiry arrival
rate with recovery headroom; receipt admission is not bounded by purge capacity today.
The raw seven-day payload lower bound is arrivals/s × 604,800 × mean payload bytes.
Actual retained space also includes admission-window placement, actual closure retention,
up to another current-epoch rotation cadence before the contiguous watermark advances,
indexes/TOAST/free space and operational backlog. Default admission/retention are one
day/seven days; do not present the lower-bound formula as total required disk.

If measured purge capacity cannot cover the intended arrival distribution, the release
decision must add a concrete supported operating envelope or improve bounded cleanup
using measured evidence. Do not shorten promised retention, delete unresolved evidence
or remove exact namespace predicates to improve benchmark figures. Production preflight
must later collect only aggregate child counts, payload size distributions, expected
write rates, retained row counts and storage/index totals under separately authorized
read-only access. No private field dumps are necessary.

## Execution ownership and proof

Parent owns builds, discovery and measurement. Reuse its isolated PostgreSQL18 fixture
and standard Release outputs; no new container, server or custom configuration. Run no
other DB lane or CPU build concurrently. Each Fact has a 15-minute operation budget,
with existing fixture cleanup and outer runner timeout still required. Exact filter:

`FullyQualifiedName~ProjectsProtectedMutationPerformanceTests&Category=LongRunning`

Confirm **2** cases on refreshed binaries before execution. JSON is written through
xUnit output to the parent's owned TRX/log; the harness has no arbitrary output-path
environment variable. Preserve the original v0 baseline/candidate JSON and extract v1
results to distinctly named files. Record HEAD, uncommitted diff, sibling SHAs, runtime,
standard configuration, server resources/settings and lack of competing workload.
No production performance or capacity claim is made before these results exist.
