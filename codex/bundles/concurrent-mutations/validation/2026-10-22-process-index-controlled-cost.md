# Controlled Process index cost investigation

Status: investigation complete; throughput regression reproduced. This is not a capacity
or activation pass. Production remains unchanged at `cbff4ac4f`, and recovery remains
disabled by default. The next implementation is specified in
[selection publication reuse](../plan/P16-process-selection-reuse.md).

## Native comparison

The exact signed pre-compact production is `4a35d5311aa23c5294b6fabadc5f4930ebd863d1`.
Its managed worktree contains only the shared diagnostic test sources. Both variants use
the same SDK, source library repositories, standard Release configuration, actual File
owner fixture and source workload: 20 warmup writes, then 100 single-attempt writes and
100 concurrent page reads. Fixture support source is identical after line-ending
normalization; every actual input and output byte identity is retained separately.

Execution order is baseline/candidate, candidate/baseline, baseline/candidate. Each of the
six runs discovers and passes one exact case. Every run accepts all 100 writes, reports
zero conflicts and verifies each final original run revision. No retry masks an outcome.

| Sample | Writes/second | Write p50 / p95 / p99, ms | Read p95 / p99, ms |
| --- | ---: | ---: | ---: |
| Baseline 1 | 9.8725 | 93.6394 / 136.5832 / 185.8749 | 380.5339 / 1404.8062 |
| Candidate 1 | 8.5522 | 109.9290 / 150.0260 / 203.8553 | 662.0753 / 970.3239 |
| Candidate 2 | 8.6237 | 106.9854 / 169.6071 / 234.1501 | 370.5140 / 627.5188 |
| Baseline 2 | 9.9744 | 93.3242 / 150.2246 / 218.8188 | 456.2454 / 1083.0811 |
| Baseline 3 | 9.8798 | 93.8044 / 129.4390 / 224.8522 | 200.2992 / 1145.0311 |
| Candidate 3 | 8.7363 | 107.1096 / 120.5277 / 249.6951 | 35.5184 / 463.0739 |

Median throughput falls 12.714% (9.8798 to 8.6237/s), beyond the unchanged 10%
investigation threshold. Median write p95 rises 9.842% / 13.4428 ms (136.5832 to
150.0260 ms). The wide read-tail variation is visible; do not claim controlled cold cache,
machine-load isolation, contention capacity or a production latency SLA from this lane.

## Attribution and rejected shortcuts

A separate one-case listener diagnostic passes on both production variants. It includes
fixture construction and warmup, so it is not another capacity sample. Existing tracing
records 740 baseline and 863 candidate JSON publications. Compact serialization accounts
for 123 calls and only 7.25 ms. Aggregate JSON-write duration rises from 13812.04 to
15337.48 ms. Key-wait totals are 636.55/632.33 ms; workspace reacquisition totals are
772.96/778.82 ms. Initial workspace wait and physical flush subphases are not separately
measured, and nested inclusive durations must not be added together.

The extra 123 publications are a measured optimization target. Reusing stable complete
selection metadata needs a canonical selection stamp, exact original targets, deterministic
content and crash-compatible validation. Simply ignoring revision changes, comparing counts,
removing fsync, bypassing publication guards or caching by file timestamp is rejected.

The .NET pattern checklist covers four hot-path files and 25 recipes. It finds no sync-over-
async, async void, uncached per-call explicit serializer setup, culture-dependent string
search or unsealed class. All seven classes are sealed. Six serializer call sites reuse
options; five LINQ selection sites and one explicit list allocation do not justify a blanket
rewrite. Complete matches and manual inverse/compound checks are retained in the proof.

## Failed attempts and proof identity

The first historical full build fails at Windows' path-length limit while copying templates.
The same managed checkout succeeds through an owned `C:/cm-cost` junction, using unchanged
source and standard Release outputs. It is not a second checkout or a custom configuration.
The diagnostic initially has an ambiguous target-typed AddOrUpdate constructor; its explicit
type correction builds. The first native listener run preserves all writes but fails the
measurement assertion because source identity was checked during static initialization.
The corrected source-name listener with both sampling callbacks passes both diagnostic runs.
Keep those failures; nine native executions comprise eight passes and that one diagnostic
failure, not nine green cases.

All three execution records have before/after identity verification. Each variant pins
566 historical or 568 current owning/copied output files; focused output identities and the
full-manifest hashes are retained. The final listener candidate has 33/34 scoped source pins
and 15 resource-source pins. Ordinary fixtures use in-memory profile setup plus actual files;
no new database, server, listener or container is created.

Scoped Persistence architecture remains at `snap-20261022215400-198deb42`: 148 types,
1530 members, 55 findings and the pre-existing HistoricalReadPass containment cycle.
Ten pre-existing NuGet-advisory MSBuild diagnostics are marked errors; the separate builds
pass. No production architecture change is claimed by this test/design checkpoint.

Full portability includes the two new test sources and passes with 15675 unchanged findings,
with final enforcement without baseline writing. Portability/redaction/rendering tool tests,
the rendering register, documentation and whitespace checks pass. The final broad Stable
suite remains at the integrated freeze, not this diagnostic-only checkpoint.

Compact proof: `proof/2026-10-22-process-index-controlled-cost.json` relative to the bundle,
342384 bytes, SHA256 `FF3CF522EF44E9E8824EB37A761225717043C86BB10D8D3961EE0715351863D6`.
The three independent resource reconciliations cover six/one/two test commands and report
zero new process, database-name or temporary-path residue. The managed baseline checkout
and its short-path junction remain explicitly owned for the next repair comparison; archive
them after their final required use. Historical denied cleanup remains untouched.

The design checkpoint permits the local index-publication repair. Contended sibling-root
progress, HTTP/status cutover, Process/Workflow refresh, remaining owners and final E2E
are still open. SPA remains on `codex/concurrent-mutations`; main is untouched.
