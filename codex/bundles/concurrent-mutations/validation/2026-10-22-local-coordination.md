# Shared local workspace admission

Status: all 90 focused cases pass, including the six original concurrent-progress cases,
the diagnostic and four owner-cost cases. Eight Release builds, final source/output
identity, architecture, full portability and observed resource reconciliation pass.
This qualifies the local admission repair; worker activation and E2E remain open.

The [selected design](../plan/P16-local-coordination-fairness.md) replaces per-store
semaphores with one process-local queue per normalized workspace-lock path. Typed control
admission covers selected execution reads and the existing authority-checked stop write.
Ordinary admission covers other operations, including the four direct recruiting-evidence
callers found by the first build. FIFO is preserved within each class; at most eight
control grants precede a waiting ordinary grant. Cancelled waiters leave the queue,
leases release once, and inactive entries leave the shared pool.

The publication scope still releases local and OS coordination before selected keys or
external authority, then reacquires with its original admission class and checks the
original witness. OS file exclusion, ordered keys, authority, journals, receipts and
default two-second recovery slices are unchanged. No public contract, project reference,
registration or new partial class was added. Recruiting code retains ordinary exclusion
with scoped leases instead of repeated semaphore `try/finally` blocks.

## Native progress result

The exact six original cases now pass, using the same real children, registered worker,
2,000 unrelated historical records, 100 single-attempt writes, held first-child key,
two-second slice and explicit five-second test lease. All 600 writes are accepted with
zero conflicts. Both roots settle their original work and allocation identities; the
held child's original bytes remain unchanged until release. Each case retains exactly
two provider calls and idle recovery adds no receipts.

| Basis | Writers | Healthy child cancelled during load | Writes finished then | Batch | Write p95 |
| --- | ---: | ---: | ---: | ---: | ---: |
| StepSnapshot | 1 | 5.075 s | 27 | 17.881 s | 210.25 ms |
| StepSnapshot | 8 | 4.936 s | 24 | 17.831 s | 1,781.80 ms |
| StepSnapshot | 32 | 5.280 s | 26 | 17.884 s | 6,167.79 ms |
| RunSnapshot | 1 | 5.188 s | 26 | 18.481 s | 238.30 ms |
| RunSnapshot | 8 | 4.939 s | 24 | 17.902 s | 1,752.18 ms |
| RunSnapshot | 32 | 5.267 s | 23 | 18.659 s | 6,654.00 ms |

The first held root consumes a bounded turn and retry pacing before the healthy root
advances. The observed approximately five seconds is not a two-second completion promise.
Compared with the [initial failure](2026-10-22-recovery-contention.md), both 32-writer tails
fall from 16.6–17.0 seconds to 6.2–6.7 seconds. Batches still allocate 4.72–4.80 GB and
publication still rewrites history-sized canonical data. These finite local observations
are not sustained capacity, cold-cache or cross-process fairness guarantees.

## Diagnostic and ordinary publication cost

The repeated RunSnapshot/32-writer diagnostic now passes: the healthy child stops after
5.456 seconds and 23 completed writes, while the held first-child key remains held. All
100 writes succeed. Of 29 real recovery steps, only the deliberately held decision step
is cancelled. Its duration is 1,735 ms; the two healthy stop steps take 1,136 and 1,126 ms.
The largest observed OS-lock wait is 22.68 ms, compared with 1.86–1.96 seconds before.

The four owner-cost rows preserve all 400 measured writes, 400 reads and 80 warmup writes
with no conflicts. These are single, cache-uncontrolled observations, not a new paired
capacity comparison.

| Historical records | Writers | Batch | Write p95 | Read p95 | Allocated |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 1 | 9.827 s | 112.63 ms | 77.92 ms | 0.195 GB |
| 2,000 | 1 | 14.316 s | 165.40 ms | 128.53 ms | 4.196 GB |
| 2,000 | 8 | 13.961 s | 1,189.80 ms | 121.09 ms | 4.235 GB |
| 2,000 | 32 | 14.166 s | 4,717.28 ms | 125.62 ms | 4.323 GB |

## Validation and retained evidence

Native discovery matches 90 cases: nine pure queue cases and 81 Integration cases. The
queue tests cover path sharing, independent workspaces, FIFO/burst limits, cancellation,
grant races and idempotent release without filesystem or runtime construction. All 81
Integration cases pass: publication/deadline, parent-stop, snapshot, recruiting API,
completion, contention, listener diagnostic and owner-cost cases.

All eight affected Release builds pass after the first persistence build exposed eight
errors in four untouched recruiting semaphore callers. That failed build is retained;
the repair changes their lease usage and preserves their behavior, with eight existing
recruiting API cases passing. Standard outputs and the existing isolated PostgreSQL
fixture are reused. Final verification confirms all 450 source pins and 50 owning/copied
outputs unchanged throughout native validation. The observer covers all 14 execution
commands and reconciliation finds no new processes, database names or fixture directories.

Scoped architecture snapshots `snap-20261022222045-198deb42` and
`snap-20261023000913-198deb42` add the internal gate, priority enum and three private queue
implementation types. The sole additional finding is informational: nine source members
in the cohesive 90-line gate. Existing type cycles and ten design-time load diagnostics
are unchanged; direct builds provide compilation evidence. No project or registration
changes were made.

Full portability scans 10,010 files, including untracked source, with 46,337 raw findings.
Review identifies one intentional protected finding: platform path normalization in the
gate. Only that fingerprint/count and baseline timestamp change. Final enforcement
without `--write-baseline` passes with 15,676 reviewed executable-source findings. The
6/4/7 validation-tool tests and rendering-register check pass. The compact
[proof](../proof/2026-10-22-local-coordination.json) retains command identities, actual
measurements, failed initial build and enforcement, source/output checks and reviews.

The diagnostic's existing `workspace.coordination.wait` activity measures OS-lock
acquisition. Local queue waiting is included in total recovery-step duration, not a
separate activity; do not describe the OS activity as total admission latency.

Existing cleanup denials remain recorded and were not retried. This run's resource
result does not close the earlier handoff folders, baseline junction/worktree or other
denied artifacts. The next independent
slice is [read-only cancellation observation](../plan/P17-cancellation-observation-and-admission.md).
Root admission/cutover, sustained and separate-process qualification, Process refresh,
Workflow/settings, remaining mutable owners and final Stable/E2E remain open. SPA stays
on its dedicated branch for colleague review.

Documentation and whitespace checks pass. The local artifact scan has three reviewed
PowerShell-source heuristic matches and zero credential-sentinel matches; the exact
compact proof scans with zero findings, with no oversized/unreadable text omitted.
This run's two bulk scan files were removed separately after identity verification;
retained hashes and summary preserve the evidence. Earlier denied targets remain.
