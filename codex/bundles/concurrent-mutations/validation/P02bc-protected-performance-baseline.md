# Protected Projects: complete baseline and canonical-length comparison

Parent execution passed both Facts in
`.artifacts/concurrent-mutations/validation/p02bc-protected-performance.trx`.
This review parses retained evidence only; it ran no benchmark. The replacement
Fact took 72.020 seconds and create/replay/capacity took 15.192 seconds (87.212
seconds summed test duration). There are 33 measured samples and 3,300 offered
operations: 2,151 new acceptances, 300 exact replays, 300 found lookups and 549
expected original-expectation conflicts. No unexpected outcome appears.

The matched post-change run is now also complete: **2 passed, 0 skipped** in
`.artifacts/concurrent-mutations/validation/p02bc-canonical-performance.trx`.
Its 33 samples preserve those exact outcomes and reduce large-definition mean
allocation by 1.016MB/attempt (7.61%). Latency is mixed, including slower small
replacement and contention samples; this is an allocation improvement, not a
demonstrated general latency improvement. The original baseline below remains
unchanged; the full comparison follows it.

This is the complete **pre-optimization** baseline. The
[earlier partial record](P02b-protected-performance-analysis.md) remains separate:
its replacement Fact failed in diagnostic JSON projection before concurrent
shapes. Its 12 uncontended samples are not pooled with this run.

## Measurement scope

Workload `protected-projects-v1`: .NET 10.0.12, Windows X64, 20 logical processors,
workstation GC, PostgreSQL 18.6, NullActivityStream. Recorded assembly version is
`1.0.0+0bdb050c4326b49aac8d9a7f4ff8b3e8c30229e1`; this identifies HEAD, not the
entire frozen working diff. Parent's build/freeze record defines that candidate.
Each shape has three samples of 100 operations after 20 warmups. Children means
each collection: 1,000 phases and 1,000 options in the large case.

Protected admission, coherent reads, exact receipts and required transactional
Search are included. HTTP/authentication, browser persistence/rendering,
governed source authority and concurrent hosted cleanup are excluded. The owner
uses a synthetic allowed LocalWorkspace receipt policy. One write-context query
per worker is reused within a sample and charged to batch time/allocation.

Values below are sample minimum–maximum, not pooled percentiles. Save timings
exclude the opening coherent read; end-to-end includes read/preparation and wave
barrier. MB/kB are decimal. Allocation includes process-wide preparation, DI,
reads and probe costs; WAL is whole-cluster LSN delta. This was parent-serialized,
but these counters are not per-request CPU or exclusively receipt WAL.

## Latency, throughput and allocation

| Shape | Writers | Children | Save p50 ms | Save p95 ms | Save p99 ms | End-to-end p95 ms | Offered/s | MB/attempt | WAL kB/attempt |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Replace | 1 | 0 | 24.34–27.83 | 32.27–46.22 | 48.47–70.09 | 39.99–51.82 | 29.20–34.75 | 0.660–0.694 | 2.02–2.10 |
| Replace | 1 | 5 | 23.29–27.63 | 47.20–51.84 | 57.30–69.63 | 50.26–58.57 | 29.73–32.67 | 0.729 | 2.92–3.01 |
| Replace | 1 | 100 | 24.42–27.76 | 30.69–45.56 | 39.00–51.83 | 35.36–50.25 | 29.13–34.08 | 1.983 | 13.41–13.45 |
| Replace | 1 | 1,000 | 39.80–45.33 | 53.83–60.63 | 57.99–122.32 | 61.24–65.54 | 19.17–21.30 | 13.352–13.353 | 110.04–110.13 |
| Disjoint replace | 8 | 5 | 29.49–43.40 | 33.83–64.24 | 34.33–65.68 | 38.19–72.91 | 146.04–219.31 | 0.749–0.752 | 2.99–3.12 |
| Same-project replace | 8 | 5 | 36.51–36.88 | 50.04–51.42 | 51.19–54.12 | 54.59–55.60 | 141.56–143.53 | 0.466 | 0.67–0.74 |
| Disjoint replace | 32 | 5 | 120.44–167.35 | 163.16–253.89 | 164.88–257.47 | 183.82–279.64 | 140.28–183.71 | 0.776–0.796 | 3.25–3.29 |
| Same-project replace | 32 | 5 | 96.88–137.42 | 152.08–191.99 | 158.57–205.15 | 166.41–204.27 | 150.11–176.39 | 0.473 | 0.60–0.65 |
| Create | 1 | 5 | 23.17–25.78 | 30.15–34.55 | 33.77–58.86 | 30.16–34.57 | 38.34–38.87 | 0.621–0.622 | 6.16–8.55 |
| Replay create | 1 | 5 | 1.53–2.04 | 1.88–3.97 | 2.81–5.82 | 1.88–3.97 | 439.14–622.93 | 0.082 | 0 observed |
| Receipt lookup | 1 | 5 | 1.58–2.00 | 2.04–3.67 | 2.60–4.42 | 2.04–3.67 | 466.06–606.56 | 0.065 | 0 observed |

All disjoint writes accepted: 300/300 at each width, without serialization-failure
outcomes. Same-project waves intentionally share original expectations: 13
acceptances/87 conflicts per 100 at width 8, and 4/96 at width 32. Their *new*
accepted rates are only 18.40–18.66/s and 6.00–7.06/s respectively; offered rate
must not be reported as useful write throughput. Replays/lookups create no new
root, Search document or receipt.

Eight disjoint workers improve throughput over one; increasing to 32 does not
improve this workload and substantially increases latency. This supports a
scaling limit in this host/workload, not a proved global-lock diagnosis. Slow
width-32 tails spread across Receipt, Children and Search families, while their
advisory-family sums are about 2.5–3.3ms. EF timing omits raw lifecycle commands,
materialization and transaction control; none of these sums isolates lock wait.
Closed-loop waves do not establish sustained arrival-rate capacity or a safe
global concurrency setting.

## Allocation and query attribution

Large replaces allocate about 18.3 times the five-child case. Across 300 large
attempts the observed process collection totals are gen0/gen1/gen2 = 256/106/19,
versus 18/2/0 for five-child replacements. Large sample maxima are 67.13, 78.58
and 159.86ms; the last overlaps 2/2/1 collections and 109.24ms in the Receipt
family. GC and receipt I/O both warrant attention; no exclusive causal share is
established. Allocation is very close to the partial run's 13.353–13.354MB, so
the repeated canonical encoding remains a source-supported candidate, not a
measured explanation of all allocation.

Replacement/create samples observe 25 commands/attempt plus opening metadata
(25.03 at one worker, 25.24 at 8, 25.96 at 32). Conflict mixes observe 19.15/19.24;
replay and lookup observe one. Batches can contain several SQL statements.
The repaired diagnostic now records the 1,000-phase plan: indexed scan using
`IX_Projects_ProjectPhases_ProjectId_OrderIndex`, incremental sort, 1,000 actual
rows versus 1 estimated on the scan, 0.035ms planning and 0.283ms execution,
924 shared hits and zero reads. This isolated warm plan does not indicate an
unindexed scan or account for complete editor materialization. The estimate
error should be observed with representative retained data/statistics before
changing indexes.

Compared with the partial run, create throughput is lower (38.34–38.87 versus
40.66–43.13/s), replay/lookup variability is wider, and large p99 spans both lower
and higher values. These are different runs/candidates; no optimization occurred
between them and no regression or improvement is attributed without matched
repeats. The partial run contains no comparable concurrent samples.

## Receipt retention and bounded cleanup

| Cleanup cell | Bytes/receipt | Seed rows | First-cycle deletion | Time | Allocated | Stop |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Five children | 2,171 | 8,193 | 8,192 | 974.965ms | 8.157MB | 64 × 128 batch cap; one remains |
| 1,000 children | 291,319 | 192 | 192 | 52.825ms | 0.705MB | Byte batches 57/57/57/21 then empty |

The immediate diagnostic second small cycle deletes the remaining row in
16.077ms. Active receipts survive. Small first-cycle WAL is 922,776 bytes;
large is 571,088 bytes. Large backlog exhaustion is not a maximum-rate result.
DELETE leaves allocated relation space: after cleanup the small table remains
18.661MB total and the large stage 38.715MB; vacuum/reclamation was not measured.

At the configured 60-second cadence and 64-batch cap, structural ceilings are
8,192 small rows/minute (136.53/s), 3,648 measured-large rows/minute (60.8/s), and
512 rows/minute (8.53/s) at the 2MiB accepted-result maximum, before time-budget,
competition and payload-distribution costs. The observed finite disjoint rates
exceed the small structural cleanup ceiling. Sustaining those arrival rates
would accumulate eligible backlog even if every cleanup cycle hit its cap.
This remains a release-capacity decision: establish the supported mixed-size
arrival envelope and backlog monitoring/response or revise bounded maintenance
with proof. A passing finite benchmark does not resolve it, and canonical-length
reuse does not change receipt size or cleanup capacity.

Seven-day raw payload lower bounds per sustained accepted write/s are 1.313GB
for small receipts and 176.190GB for large receipts. At an assumed 100/s those
are 131.302GB and 17.619TB. These are modelled payload totals, not production disk
forecasts: admission/closure windows, watermark delay, heap/index/TOAST overhead,
compression, retained distribution and backlog remain additional factors. No
ordinary database or production retained-data distribution was inspected.

## Matched canonical-length run

The parent built and ran the candidate that retains the first hard-v1 canonical
byte length and compares it with the current limit only after an original
receipt miss. Canonical fields, digest, result payload and transaction ordering
are unchanged. The parent reports codec **25/25 passed**, including the escaped
Unicode exact/one-less boundary, and the HTTP lane passed its new actual-owner
exact-boundary/replay-under-lower-limit/fresh-operation-refusal case. These are
execution reports from the parent; this review only parsed retained TRX output.

The repeat records the same workload version, runtime, OS, processor count, GC,
PostgreSQL, activity stream, warmups, sample sizes and context policy. The assembly
HEAD identity is also unchanged; as above, it does not identify the complete
working diff. This is one sequential run per candidate, not randomized paired
trials or statistical isolation from host/database background activity.

All 33 corresponding samples have identical attempts, accepted counts, observed
commands/attempt, added receipt rows and payload bytes/new acceptance. Both runs
have 2,151 new acceptances/receipts, 549 expected conflicts, 300 exact replays and
300 found lookups, with no unexpected outcome. Every disjoint attempt accepts.
Same-project waves retain 13/87 and 4/96 acceptance/conflict splits at widths 8
and 32. Replays/lookups still add no receipt or project effects.

The repeat replacement Fact took 78.347s and create/replay/capacity 14.690s,
summing to 93.037s versus 87.212s before. Fact duration includes setup, warmups,
diagnostics and cleanup; it is not a request-latency measurement.

### Allocation across all shapes

Values are arithmetic means of three equal-size samples, in decimal MB per
offered attempt. The percentage is the change in those means. Process-wide
measurement limits still apply; tiny replay/lookup differences are not a gain
claim, and cold/setup-sensitive zero-child allocation varies across samples.

| Shape | Writers | Children | Before MB | After MB | Change |
| --- | ---: | ---: | ---: | ---: | ---: |
| Replace | 1 | 0 | 0.676826 | 0.659512 | -2.56% |
| Replace | 1 | 5 | 0.729032 | 0.714643 | -1.97% |
| Replace | 1 | 100 | 1.982726 | 1.855842 | -6.40% |
| Replace | 1 | 1,000 | 13.352690 | 12.336433 | -7.61% |
| Disjoint replace | 8 | 5 | 0.750185 | 0.740569 | -1.28% |
| Same-project replace | 8 | 5 | 0.466422 | 0.452295 | -3.03% |
| Disjoint replace | 32 | 5 | 0.782929 | 0.767264 | -2.00% |
| Same-project replace | 32 | 5 | 0.472978 | 0.458830 | -2.99% |
| Create | 1 | 5 | 0.621668 | 0.609797 | -1.91% |
| Replay create | 1 | 5 | 0.081543 | 0.081508 | -0.04% |
| Receipt lookup | 1 | 5 | 0.065193 | 0.065177 | -0.02% |

The 100-child samples consistently fall from about 1.983 to 1.856MB; the
1,000-child samples fall from 13.352–13.353 to 12.336–12.337MB. That size-dependent
reduction agrees with removing the duplicate canonical allocation/hash, while
the unaffected replay/lookup paths remain essentially flat. Large-definition
gen0/gen1/gen2 totals move from 256/106/19 to 250/99/17 across 300 attempts;
five-child replacement remains 18/2/0. Significant allocation and GC remain, so
this does not explain or remove all large-definition cost.

### Repeat latency and offered rates

Ranges again show the minimum–maximum of the three sample statistics, not
pooled percentiles. The table covers all 33 samples and is comparable with the
baseline table above.

| Shape | Writers | Children | Save p50 ms | Save p95 ms | Save p99 ms | End-to-end p95 ms | Offered/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Replace | 1 | 0 | 26.30–32.42 | 39.99–96.29 | 55.51–110.78 | 45.14–107.33 | 22.76–29.00 |
| Replace | 1 | 5 | 25.26–32.47 | 53.55–81.95 | 70.55–114.35 | 61.12–93.29 | 22.29–29.84 |
| Replace | 1 | 100 | 25.37–28.18 | 29.48–53.01 | 34.03–66.69 | 33.88–60.68 | 27.21–34.36 |
| Replace | 1 | 1,000 | 40.36–41.43 | 49.10–59.40 | 58.83–69.47 | 53.92–64.16 | 20.62–21.31 |
| Disjoint replace | 8 | 5 | 34.80–36.95 | 39.56–42.90 | 39.84–43.40 | 44.45–49.10 | 174.34–187.19 |
| Same-project replace | 8 | 5 | 36.61–40.68 | 52.49–56.76 | 55.97–58.55 | 56.46–61.02 | 131.08–142.24 |
| Disjoint replace | 32 | 5 | 138.88–174.91 | 185.53–200.78 | 186.85–210.58 | 202.66–254.38 | 140.35–144.39 |
| Same-project replace | 32 | 5 | 111.50–142.85 | 168.01–246.46 | 175.39–268.85 | 184.81–263.67 | 126.03–152.15 |
| Create | 1 | 5 | 24.54–26.16 | 29.57–31.34 | 31.27–57.00 | 29.59–31.35 | 37.47–39.96 |
| Replay create | 1 | 5 | 1.74–2.10 | 2.19–2.92 | 2.33–3.86 | 2.20–2.93 | 449.21–557.33 |
| Receipt lookup | 1 | 5 | 1.82–2.12 | 2.40–2.86 | 2.67–3.28 | 2.40–2.86 | 455.98–533.01 |

Large-definition latency improves in parts of this repeat, with p95 ranges
overlapping and a narrower p99 range. Other shapes do not establish a general
gain. Five-child replacement p95 is worse in every repeat sample than in every
baseline sample; zero-child tails also worsen. Width-32 same-project p99 rises
from 158.57–205.15 to 175.39–268.85ms. Useful accepted rates in same-project mixes
are 17.04–18.49/s at width 8 and 5.04–6.09/s at width 32, rather than their offered
rates. Disjoint width 32 still fails to improve on width 8.

The repeat's worst five-child save is 287.96ms, with no observed overlapping GC
and 198.65ms in observed SQL families spread across Root, Children, Other and
others. Its worst zero-child save is 185.32ms with no overlapping GC and only
57.25ms in observed SQL. This rules out attributing those particular tails to
observed collections, but does not identify scheduling, unobserved transaction
work or server/I/O causes. Width-32 same-project worst calls contain
133.93–208.22ms in the advisory family, consistent with deliberate same-target
serialization. Large worst calls overlap gen2 collections. No profile or server
wait sample was captured; the source change alone neither explains these
variations nor proves latency non-regression. Further latency promises require
repeat/alternating measurements under a defined SLO and load envelope.

### Retention and diagnostic comparison

Receipt sizes, row counts, command counts and structural maintenance ceilings
are unchanged. The small first cleanup deletes the same 8,192 rows in 725.160ms
versus 974.965ms, allocating 8.156 versus 8.157MB; its final one-row cleanup takes
18.183 versus 16.077ms. Large cleanup deletes the same 192 rows in the same
57/57/57/21/0 batches in 50.038 versus 52.825ms, allocating 0.708 versus 0.705MB.
Cleanup does not call the changed digest path, so its timing variation is not
an optimization benefit. Active receipts remain retained and allocated relation
space is not reclaimed by the measured DELETEs.

The repeat's indexed 1,000-phase diagnostic still estimates one scan row and
returns 1,000, with 935 shared hits/zero reads, 0.106ms planning and 0.623ms
execution. It does not change the prior index/statistics conclusion. Cluster
WAL varies independently: one lookup sample observes 56 bytes with no write
effects, and one width-32 same-project sample observes 668,408 bytes versus
59,920/62,040 in its other samples. These deltas must not be attributed to added
receipt writes or the removed in-process hash. Retention payload estimates and
the backlog-capacity concern above remain unchanged.

## Remaining acceptance

The implemented change has a measured bounded allocation benefit and unchanged
functional sample outcomes. Retain that narrow conclusion; it does not close
latency, sustained-capacity or end-to-end performance acceptance.

No hard latency/throughput SLO is asserted by these Facts. Remaining evidence
includes mixed-load maintenance/backlog behavior, maximum-byte fields/receipts,
large replay, governed authority and real HTTP/client overhead. Preserve fixture
ownership/cleanup records and final static enforcement. This document records a
complete baseline and one matched repeat, not end-to-end performance closure.
