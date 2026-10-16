# Protected Projects: partial baseline and harness repair

Status: reviewed partial measurement; complete concurrent baseline and repaired-harness validation pending. The parent ran the two `ProjectsProtectedMutationPerformanceTests` Facts from standard Release outputs against its isolated PostgreSQL fixture. This analysis launched no workload and changed no production source.

Evidence: `.artifacts/concurrent-mutations/validation/p02b-protected-performance.trx`. Extract only the result name/outcome/duration/error and the structured records' workload, sample, timing, outcome, allocation, GC, command-family, receipt-size, WAL and maintenance fields. Do not publish raw SQL, parameter values, environment credentials or arbitrary TRX content.

## Result and exact failure

| Fact | Outcome | Duration | Completed evidence |
| --- | --- | --- | --- |
| `Measure_protected_replacements_with_matched_shapes_and_tail_diagnostics` | Failed in diagnostic projection | 60.656 s | All 12 uncontended samples: 1,200/1,200 accepted; 240 preceding warmup attempts completed. No 8/32-writer disjoint/same-project samples ran. |
| `Measure_protected_create_replay_lookup_and_bounded_receipt_cleanup_capacity` | Passed | 13.525 s | 300 accepted creates, 300 exact-envelope replays, 300 found receipt lookups, 60 create warmups and both bounded cleanup cells. |

The failure is `System.FormatException` from `JsonElement.GetInt64()` inside `ProjectsOperationStorageMeasurement.ChildReadPlanAsync`, after the last 1,000-child sample and before concurrent setup. It is not a transaction, CAS, timeout or performance-threshold failure. PostgreSQL 18 emits `Actual Rows` as a fractional per-loop average with two decimal places; the harness demanded an integer JSON token. PostgreSQL's [version-18 implementation](https://github.com/postgres/postgres/blob/REL_18_STABLE/src/backend/commands/explain.c#L1733-L1758) confirms `Actual Rows` uses two decimal places while `Actual Loops` and `Plan Rows` use zero. The original raw plan was not retained because projection failed.

Harness repair: read only `Actual Rows` as `double`, leaving loop/estimated-row/buffer counters as integers. Extract the existing projection into `SummarizeChildReadPlan(JsonElement)` so one pure regression, `ProjectsOperationPerformanceProbeTests.Explain_summary_preserves_fractional_actual_rows_and_only_approved_plan_fields`, covers decimal whole/fractional rows, recursion and exclusion of private predicates. The two LongRunning cases retain their names/counts; the new parser class adds one ordinary Fact. No suppression, fallback/default row count or production change was introduced. Parent rebuild, focused regression, complete repeat and repository static gate remain required.

## Environment and comparable scope

Recorded workload is `protected-projects-v1`, .NET 10.0.12, Windows X64, 20 logical processors, workstation GC (`ServerGc=false`), PostgreSQL 18.6, `NullActivityStream`. Both assembly informational versions identify `1452352f07d14a4b526ae3840bba298ea11db1a0`; that identifies HEAD, not the entire uncommitted measured source. Preserve the parent's frozen diff/build record with the TRX. Each cell has three samples of 100 offered operations after 20 warmups. The count is children **per collection**: the large definition has 1,000 phases plus 1,000 options.

Measurements include protected owner writes, required transactional Search, full exact accepted receipts, original expectation CAS and dataset/epoch guards. Caller policy is synthetic LocalWorkspace with allowed receipt reads. HTTP/JWT, browser storage/rendering and governed source-authority costs are excluded. The historical operation-free baseline is not a directly interchangeable protocol baseline.

## Completed timings and costs

Ranges below are the minimum and maximum across the three samples, not pooled percentiles. MB and kB are decimal. Allocation is process allocation per offered operation including coherent read, preparation, DI and probe overhead.

| Operation | Children/collection | Save p50 ms | Save p95 ms | Batch operations/s | Allocated MB/operation | Accepted payload bytes | WAL bytes/operation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Replace | 0 | 20.97–30.14 | 34.87–62.61 | 26.15–39.06 | 0.659–0.694 | 758.9 mean | 2,057–2,102 |
| Replace | 5 | 22.32–22.86 | 43.56–63.34 | 33.52–34.41 | 0.729 | 2,160.7 mean | 2,887–4,874 |
| Replace | 100 | 23.94–24.39 | 31.48–65.36 | 29.99–34.28 | 1.983 | 29,407.7 mean | 13,422–13,497 |
| Replace | 1,000 | 38.40–44.27 | 59.09–69.39 | 18.66–21.24 | 13.353–13.354 | 291,308.7 mean | 110,006–110,043 |
| Create | 5 | 20.74–21.27 | 27.46–47.91 | 40.66–43.13 | 0.621–0.622 | 2,171 | 6,127–6,292 |
| Replay create | 5 | 1.42–1.52 | 1.84–2.33 | 624.98–649.79 | 0.082 | No new receipt | 0 observed |
| Lookup | 5 | 1.40–1.59 | 1.74–2.20 | 587.45–671.04 | 0.065 | No new receipt | 0 observed |

The large replacement p99 values were 73.46, 84.75 and 93.16 ms; sample maxima were 82.62, 127.70 and 121.30 ms. The first zero-child sample is materially slower than its later samples; warmup does not establish fully stationary JIT/cache behavior. No hard latency target is enforced by this harness.

Every measured replacement/create sample observed 25.03 EF commands per operation, including three sample-opening metadata commands spread across 100 operations. Replay/lookup each observed one EF command per operation. These are not complete SQL counts: raw lifecycle commands and transaction control are not intercepted.

## Attribution supported by this run

1. **Definition-size allocation is material.** Large replacement allocation averages 13.354 MB versus 0.729 MB at five children, approximately 18.3 times as much. Large samples had gen0/gen1/gen2 collections of `88/38/8`, `84/30/5` and `84/31/5`; small replacement samples had no gen2 collection. Seven of the 15 retained large tails overlap a gen2 collection, but the slowest tail (127.70 ms) does not. GC pressure is supported; GC as the single latency cause is not.
2. **Receipt command time grows with exact result size.** The receipt command family averages 8.78–9.58 ms per large offered replacement versus 3.14–3.31 ms for five children. Each contains three observed receipt commands, not just INSERT. Other observed command families also consume time. The sum over all observed families averages 27.73 ms per large offered replacement versus 19.94 ms for five children, including the coherent opening read. It must not be subtracted directly from save-only duration; one retained tail's summed commands exceed its save duration because that sum includes its read.
3. **The repeated canonical encode is a concrete avoidable candidate.** `ProjectDefinitionOperationProtocol.PrepareAsync` calls `ComputeDigest` under supported v1 hard limits; a new effect calls it again in `ProjectDefinitionWriter.ExecuteProtectedAsync` only to enforce current canonical-byte limits. The immutable intent/operation/caller are unchanged. Each encode builds a bounded `MemoryStream`, obtains a full `ToArray`, then hashes it. This is a source-supported duplicate traversal/allocation, not a measured percentage attribution.
4. **Accepted bytes make several owned copies.** `EncodeAccepted` creates the encoded array; `ProjectDefinitionEncodedResult` copies it; `InsertAsync` requests another copy. Decode also copies input and re-encodes to prove canonical equality. These preserve ownership today. The current replay sample has only five children, so it does not measure large-receipt replay allocation/tails.
5. **No observed concurrency bottleneck conclusion is available.** The failure precedes the 8/32-writer shapes. Advisory duration cannot prove lock contention and this run cannot establish disjoint-project scaling. The child-read plan is unavailable until the repaired repeat. Current writer child matching uses dictionaries; source inspection does not justify a quadratic-child-search claim.

## Smallest proposed optimization, after the complete baseline

First retain the canonical byte count produced by the existing hard-limit digest encode in the operation attempt. On `NotObserved/Open`, enforce the current deployment `MaxCanonicalCommandBytes` against that exact retained count rather than serializing and hashing the identical immutable intent a second time. Keep the current raw/count bounds check for new effects and the existing first hard-limit encode/digest. Existing accepted receipt lookup must still happen before deployment-limit checks; tighter deployment limits must not prevent historical replay.

This changes no digest bytes, versions, semantic inputs, owner transaction, current authorization, result payload or retention promise. Minimal source scope is codec digest result/byte-count plumbing, operation attempt and the one redundant writer call. Required proof: golden digest unchanged; mutable-input isolation remains; a new command over the current canonical limit is refused; the same previously accepted command replays after that limit is tightened; hard v1 limits still reject before unbounded allocation. Re-run the same full workload and compare large allocation plus p50/p95/p99 and all outcome counts. Do not claim a speedup before that comparison.

If large allocation remains material, collect a bounded allocation/GC trace of this owned lane before changing accepted-byte ownership. A later codec-only ownership optimization may remove an internal duplicate copy using explicit ownership transfer of a freshly created array; retain defensive public/external copies and canonical-byte validation. Do not remove exact acknowledgements, shrink child mappings, refresh expectations, weaken caller/effect binding, skip Search, or shorten retention to improve figures.

## Cleanup and retained capacity

| Cleanup-only cell | Payload bytes/receipt | Seeded rows | First-cycle deletion | Duration | Stop and bound |
| --- | ---: | ---: | ---: | ---: | --- |
| 5 children/collection | 2,171 | 8,193 | 8,192 | 823.78 ms | 64 batches × 128; one row remains; second immediate diagnostic cycle removes it |
| 1,000 children/collection | 291,319 | 192 | 192 | 42.55 ms | Byte-limited batches `57/57/57/21`, then empty; backlog exhausted |

Active receipts survived both cells. Replay/lookup added no project, Search document or receipt rows. Small-cell purge hits the structural 8,192-row scheduled-cycle ceiling below its 2-second budget; this is one isolated cleanup sample, not sustainable mixed-load capacity. Large-cell purge exhausts its small backlog, so 192/minute is an observed finite workload, not maximum capacity. Its configured structural ceiling is 3,648 rows/minute (57 × 64), before time, competing work or distribution effects. A 100 writes/s arrival assumption exceeds that ceiling for receipts of this size. At the 2 MiB hard maximum the structural ceiling is 512 rows/minute. A supported operating envelope is still required.

Seven-day raw payload lower bounds from the measured payloads are 1.313 GB per sustained write/s for the small cell and 176.190 GB per sustained write/s for the large cell. At 100 writes/s these become 131.302 GB and 17.619 TB. These are assumptions, not observed production rates or required total disk. Admission-window placement, closed-epoch retention, delayed watermark advancement, heap/index/TOAST allocation and backlog add costs. Synthetic cloned payloads do not establish a production compression or size distribution. DELETE left allocated relation space in place, as expected; vacuum and disk reclamation were not measured.

## Harness validity and remaining gates

- Original read expectations are retained through each wave; all reads finish before writes, no retry normalizes conflict counts. This supports the designed overlap test once its unexecuted shapes run.
- Opening context queries/time/allocation are included in batch totals, with the one-per-worker reuse stated explicitly. Per-call save latency excludes that shared opening; read-plus-save latency includes command construction and the wave barrier.
- `GC.GetTotalAllocatedBytes` and collection counts are process-wide; cluster WAL LSN is cluster-wide. Parent serialization/isolation is necessary and the TRX alone cannot establish absence of another process. EF reader duration excludes subsequent materialization. SQL batches can contain multiple statements. Tail command-family sums are not disjoint CPU/SQL/lock timing.
- Samples are closed-loop fixed waves, not a sustained arrival-rate/open-loop benchmark. They do not measure production queueing, UI think time, process-authority contention, ordinary hosted maintenance overlap, or behavior at advertised maximum field byte lengths.
- Synthetic inactive cleanup namespaces and current receipts share only the owned leased database. Each cell removes its exact residual namespace; surrounding `TestApplication` owns whole-fixture cleanup if an assertion aborts the cell. No ordinary profile was inspected.
- Keep the failed TRX as partial evidence. After parser verification, rerun both Facts from rebuilt standard Release outputs, recording all 24 replacement and nine create/replay/lookup samples and the representative child plan. Do not merge the first 12 samples into a later run as though they formed one stationary baseline.
- Parent owns build, parser Fact execution, LongRunning repeat, current portability-static enforcement and final resource cleanup. No claim of repaired passing validation or complete P02 performance closure is made here.
