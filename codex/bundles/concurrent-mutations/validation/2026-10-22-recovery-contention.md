# Native recovery under competing workspace writes

Status: concurrent-progress gate failed; investigation and repair remain active.
The worker stays disabled by default. HTTP cutover, activation, broader capacity and
final E2E cannot rely on the earlier isolated-wait/backlog checks for this combination.

## Contract and first result

The [reviewed design](../plan/P16-contended-recovery.md) uses two actual inherited planning
children, PostgreSQL recovery ownership and the registered worker in one organization
workspace. One child's publication key remains held while 100 single-attempt updates
compete over 2,000 unrelated historical records. The healthy child must durably cancel
before the finite batch finishes. The two-second slice is unchanged; the five-second
lease is an explicit test setting. No owner is replaced with a fake.

| Read basis | Writers | Cancellation during batch | Batch duration | Write p95 |
| --- | ---: | --- | ---: | ---: |
| StepSnapshot | 1 | Pass, after 83 writes at 14.973 s | 17.922 s | 248.88 ms |
| StepSnapshot | 8 | Fail | 17.270 s | 4,384.75 ms |
| StepSnapshot | 32 | Fail | 20.660 s | 17,006.87 ms |
| RunSnapshot | 1 | Fail | 17.400 s | 221.76 ms |
| RunSnapshot | 8 | Fail | 17.670 s | 4,464.16 ms |
| RunSnapshot | 32 | Fail | 21.564 s | 16,611.70 ms |

All six cases preserve every accepted revision: 600 accepted writes, zero conflicts.
After releasing the held key, both roots settle their original allocations and exact
closed work in every case. Original barrier identities, cancelled decision/scope state,
two provider calls and idle replay without new receipts all pass. Five rows then fail
the separate concurrent-progress assertion. This is a liveness failure, not lost writes.
Allocations during the measured batches range from 4.52 GB to 5.17 GB; cache state and
background machine load were not controlled. These observations are not a capacity SLA.

Both existing completion regression rows and all three scenario-clock tests pass.
The fixture's live-clock option leaves its existing frozen default unchanged. Native
discovery matches 11 cases; six pass and five fail. The first build failed for a missing
test namespace import, corrected before discovery. No production implementation changed.

## Evidence and next decision

[Initial durable proof](../proof/2026-10-22-recovery-contention-initial.json) retains all
11 outcomes, failed build, exact commands and log hashes, source pins, source snapshots,
discovery, 35 owning/copied output identities and resource reconciliation. All pins remain
unchanged across execution. The observer covers all three commands and reports zero new
process, database-name or fixture-directory residue. Standard Release and the existing
isolated PostgreSQL fixture were reused.

The [diagnostic proof](../proof/2026-10-22-recovery-contention-activity.json) reproduces the
RunSnapshot/32-writer failure. Earlier attempts spend 1.86–1.96 seconds in a single
workspace coordination wait and exhaust their slice; successful observed reads deserialize
JSON in 2–6 ms. Both roots reach decision revision 4 but cannot stop their child during
the batch. After load subsides, actual stop steps take 0.61–0.63 seconds. Source and 35
output identities remain unchanged; this diagnostic also leaves zero observed resource
deltas. Nested activity durations are inclusive and must not be added together.

The [local admission design](../plan/P16-local-coordination-fairness.md) now owns the
repair. Keep the existing lock and authority protocol, preserve bounded opportunities for
ordinary writes, and prove the original assertions with the same two-second deadline.
Cross-process scheduling and sustained capacity remain explicit limits. HTTP cutover and
activation stay blocked by the failed concurrent-progress gate until repaired and tested.

Full portability passes without changing its 15,675-finding baseline. Tool tests (6/4/7),
rendering register, documentation and whitespace checks pass. Both complete local proof
roots were scanned with the private PostgreSQL value supplied as a sentinel: none was
found and no oversized or unreadable text was skipped. Each root reports three reviewed
false positives in `Execute.ps1`: a private file read and variable interpolation, not
credential values. The exact two durable proof copies pass with zero findings.

The approved compatibility cleanup removed both temporary probe sources and refreshed
Integration Release. Separate deletion of the two handoff workspaces and baseline
junction was still rejected by automatic approval review with `blocked by policy`.
Exact retry outcomes and restored production output identities are included in the proof;
these paths and the baseline checkout remain, alongside previously recorded blockers.
Automatic approval review also rejected removal of `portability-scan-1.json` and
`portability-scan-1.csv` in `c4-recovery-contention-activity`. Their 31,426,134 and
17,985,498 bytes remain with recorded hashes; no denied cleanup was silently retried.
