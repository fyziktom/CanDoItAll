# Projects performance baseline procedure

Status: baseline measured before Projects production changes. Exact discovery was one
Fact; it passed in 73 seconds with all 24 sample records. This is a successful measurement
run, not a correctness pass: disjoint saves exposed substantial PostgreSQL serialization
failures that reopen the transaction/performance design.

## Scope and selection

The single opt-in integration case is
`CanDoItAll.Tests.Integration.ProjectStructure.ProjectsMutationPerformanceTests.Measure_real_project_editor_reads_and_saves`
in `tests/Integration/CanDoItAll.Tests.Integration/ProjectsMutationPerformanceTests.cs`.
Its class carries `Category=LongRunning`, which the stable gate excludes.
Expected source discovery is **1 Fact**; confirm against the refreshed assembly.

It uses the existing `TestApplication` bootstrap and its existing PostgreSQL database
lease. Configure the parent's isolated PostgreSQL 18 endpoint through
`CANDOITALL_TESTS_POSTGRES_CONNECTION`, privately. It never creates a container or uses
the ordinary application/database. The application provider, lease and temporary
environment are disposed by existing fixture cleanup. No replacement fixture is needed.

Run only this selection, with no other test process or application benchmark sharing the
fixture or machine during measurement. Serialize builds that share standard outputs.
After the owning Release build, discover and run:

```powershell
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --no-restore --list-tests --filter "FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectsMutationPerformanceTests&Category=LongRunning" /m:1
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~CanDoItAll.Tests.Integration.ProjectStructure.ProjectsMutationPerformanceTests&Category=LongRunning" --logger "trx;LogFileName=projects-performance-before.trx" --results-directory ./.artifacts/concurrent-mutations/performance /m:1
```

The example results directory must first be recorded as bundle-owned in `resources.md`.
Use the parent's existing owned evidence directory if it differs. The harness writes
compact JSON through xUnit output, retained in the TRX; it does not accept arbitrary
artifact paths or create independent report files. Preserve reviewed JSON/TRX evidence,
not copies of compiled output. Follow the normal portability-static closure procedure.

## Workload

All measured operations call the real `ProjectsService.GetAsync` followed by
`SaveEditorAsync`. Creation is outside measurement. No mock service, synthetic
in-memory persistence, disabled search projection, retry or conflict rebasing is used.

| Scenario | Persisted phases / options per project | Writers | Samples |
| --- | --- | --- | --- |
| Uncontended | 0 / 0, 5 / 5, 100 / 100, 1000 / 1000 | 1 | 3 per size |
| Disjoint projects | 5 / 5 | 8 and 32 | 3 per width |
| Same project | 5 / 5 | 8 and 32 | 3 per width |

Each sample follows 20 warmup attempts and contains exactly 100 measured attempts total,
including a smaller final wave when 100 is not divisible by the writer count. There are
24 measured samples, 2400 measured attempts and 480 warmup attempts. Setup creates 36
projects and 1265 phases plus 1265 persisted options in one leased database.

A wave completes all reads before it starts the saves. Every worker has its own DI scope
and immutable-to-other-workers editor instance. Concurrent same-project drafts therefore
come from the same pre-wave state; the later protected implementation should reject stale
drafts rather than silently replay them. Uncontended saves change the objective and, when
present, the first phase goal and first persisted option notes. The entire editor travels
through the existing save path, exercising collection matching while the number of actual
changed children stays small. Default empty category entries returned by `GetAsync` are
reported separately from persisted option counts.

The ten-minute cancellation budget covers workload operations and service calls. Fixture
bootstrap and teardown retain their existing bounded SQL/administration deadlines; this
is not a replacement for an outer test-runner hang timeout. There are no latency assertions.
An uncontended failure fails the test after emitting that sample. Concurrent rejection and
exception counts are retained, with at least one accepted save and observed Projects and
Search commands required per sample. A completed concurrent benchmark is **not** a
correctness claim when its output contains faults.

## Measurements and interpretation

Output consists of one environment JSON record and 24 sample JSON records on success.
Environment metadata includes runtime, OS, process architecture, processor count, server
GC, PostgreSQL server version, Projects/harness assembly informational versions, and the
resolved activity-stream type. Capture the exact repository HEAD, working diff, sibling
SHAs, SDK, Release/asset mode and isolated server configuration beside the output; assembly
informational versions alone do not identify uncommitted changes.

Each sample reports:

- Read and save p50/p95/p99/max, plus separate accepted/rejected/faulted save distributions.
- Attempted and accepted operations/second, accepted/rejected/faulted counts and sanitized
  error-code/exception-type/SQLSTATE counts. It never prints SQL parameters, draft contents,
  connection strings or exception messages. Read failures fail the run directly.
- Process-wide allocated bytes and generation 0/1/2 GC counts. These include harness and
  interception costs; they are not per-thread or per-request attribution.
- Example editor JSON bytes measured outside the sample. This is a model-size proxy, not
  HTTP wire bytes or every operation's actual payload size.
- Observed completed/failed asynchronous EF command executions by Projects/Search owner,
  summed command duration, and advisory-command count/summed duration. An EF command may
  contain multiple SQL statements; these are not statement or row counts. Reader-command
  timing does not include all row enumeration/materialization. Parallel command durations
  overlap and must not be interpreted as wall time.
- A diagnostic current-64-stripe occupancy histogram over distinct project keys. The
  process uses randomized string hashing, so collision topology can change across runs
  even with identical project IDs. The diagnostic deliberately assumes the current
  implementation; update it if stripe policy changes. It never reads private locks or
  alters lock acquisition. Compare disjoint throughput with this histogram and variation.

Test-only `ConfigureDbContext` adds one thread-safe interceptor to the registered
Projects and Search options. `SearchIndexService.UpsertForMutationAsync` passes those
options through `CoordinatedDatabaseTransaction.CreateEnlistedAsync`, so the enlisted
search context retains observation. The explicit search-command assertion detects an
accidentally uninstrumented projection path. Counts remain **observed owner commands**,
not an assertion that every possible future participant is instrumented.

Save timing includes the current postcommit search projection. The current bootstrap
resolves `NullActivityStream`; its type is recorded rather than claiming persisted
activity cost. The current service can report an accepted authoritative save after a
projection failure; observed failed SQL is therefore relevant even when Accepted is 100.
Service latency excludes resolving the per-operation scope; sample throughput includes
scope orchestration, wave coordination and all reads/saves. SQL diagnostics add bounded
measurement overhead and must be enabled identically in A/B runs.

Pure lock wait, lock hold, transaction duration, connection-pool wait, rows read/written,
WAL, server CPU, cold-start behavior and two-process concurrency are unavailable here.
Advisory-command duration includes server execution and network round trips; process
semaphore waiting happens before that command and is only reflected in save wall time.
The full performance plan's delayed locks, forced stripe collision, independent-host
races and fault scenarios remain separate work. This first baseline cannot close them.

Compare the three samples and absolute differences before judging the design plan's
15% uncontended p95 / 10% disjoint-throughput investigation thresholds. A lower average
latency caused by rejecting more drafts is not a faster successful save. Keep accepted
throughput and accepted latency separate from attempts and rejections. Re-run the same
harness after Projects changes, preserving dataset shape, environment and asset mode.

## Baseline evidence and findings

Projects production source was unchanged from architecture commit `ceb10731e`; the
P01a cleanup was present in Infrastructure. Assembly informational versions identify
`ceb10731e` because the serialized builds preceded the cleanup commit `efbebfc54`.
The only measurement addition was this harness and its test-only interceptors.
Runtime: .NET 10.0.12, SDK 10.0.303, Windows x64, 20 logical processors, workstation GC.
The owned PostgreSQL 18.6 container was limited to two CPUs and 2 GiB; no ordinary
application/database was used. Activity resolved to `NullActivityStream`.

Build-backed discovery and the no-build run used the exact fully qualified filter
above, ordinary Release outputs, and the owned `validation` results directory.
Result: one passed, zero failed/skipped; 24 samples and 2,400 measured operations.
Artifacts: `.artifacts/concurrent-mutations/validation/projects-performance-baseline.trx`
and its extracted `projects-performance-baseline.json`. Both Projects and enlisted
Search commands were observed in every sample.

| Scenario | Children per collection | Writers | Save p95 range across three samples (ms) | Accepted / 300 | Faults / 300 | Aggregate accepted/second |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Uncontended | 0 | 1 | 17.58–47.72 | 300 | 0 | 38.48 |
| Uncontended | 5 | 1 | 49.55–53.68 | 300 | 0 | 30.50 |
| Uncontended | 100 | 1 | 32.18–42.22 | 300 | 0 | 35.12 |
| Uncontended | 1000 | 1 | 35.47–38.73 | 300 | 0 | 26.45 |
| Disjoint projects | 5 | 8 | 16.91–18.08 | 111 | 189 | 49.06 |
| Disjoint projects | 5 | 32 | 15.18–27.06 | 148 | 152 | 84.90 |
| Same project | 5 | 8 | 96.97–139.39 | 300 | 0 | 58.21 |
| Same project | 5 | 32 | 516.25–745.04 | 300 | 0 | 45.65 |

Every surfaced fault was PostgreSQL SQLSTATE `40001` (serialization failure). The
eight disjoint keys occupied eight different local stripes, so stripe collisions do
not explain those failures. The precise SQL/predicate-lock cause requires a focused
diagnostic before choosing an owner transaction change. Do not hide this result with
automatic retries, count failed attempts as throughput, or downgrade global isolation.
Search also recorded failed commands after some authoritative saves had returned
success, confirming that acceptance and current projection consistency differ.

All same-project stale proposals were accepted. They were independent original reads
completed before their wave's writes, so the current serialized write path does not
prevent stale overwrite. The protected candidate must reject superseded originals.
Uncontended saves observed 12 asynchronous owner SQL commands per read/save attempt.
The initial small-project timing varies substantially; these preliminary samples are
not a precise production p99 or evidence that large aggregates are cost-free.

The TestApplication/lease disposed successfully and the owned server remains available
for the focused diagnosis and candidate comparison. Harness portability enforcement
will be recorded with its source checkpoint; no baseline allowance is pre-approved.
