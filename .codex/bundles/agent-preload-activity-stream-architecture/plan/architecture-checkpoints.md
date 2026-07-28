# Architecture Checkpoints

| Checkpoint | When | Required evidence | Blocking decision |
| --- | --- | --- | --- |
| A0 preparation readiness | Before product edits | Current-state inventory, boundary/dependency maps, pattern records, testability plan, prepared validator | Any placeholder, unmapped requirement, or unresolved owner blocks SB01. |
| A1 baseline lock | End SB01 | Targeted CodeAnalytics snapshot, cold/warm baseline, existing-behavior characterization, constructor inventory | Missing reproducibility or hidden production path blocks SB02. |
| A2 stream foundation | End SB02 | Ordering/isolation/gap/cancel/dispose/error tests, producer-consumer-lifecycle matrix | Cross-stream leakage, silent loss, handler coupling, or unclear lifetime blocks SB03. |
| A3 preparation safety | End SB03 | Immutability/invalidation/cancellation/resource audit, dependency snapshot | Any live/secret/transient state in blueprint or stale commit blocks SB04. |
| A4 module snapshot safety | End SB04 | Atomic publication/content/coverage/profile/freshness tests, exact attachment-eligibility dispatch and digest proof, structural snapshot/write separation, field-complete process revision vector, no-write-back proof | Mixed identity, hidden fallback/deep load, any snapshot-fed write path, incomplete process facts, digest mismatch, or canonical write-back blocks SB05. |
| A5 backend go/no-go | End SB05 | Before/after metrics, operation counts, concurrency/EF/file-lock proof, targeted builds/tests | No material improvement, unsafe task parallelism, or unresolved regression blocks all UI work. |
| A6 UI projection | End SB06 | Component tests, Playwright logs/screenshots, typed-state review | Selected-run/string parsing, stale status, layout regression, or missing process parity blocks SB07. |
| A7 closure | End SB07 | SharedInfo validation, mini-model run, solution build/tests, final architecture snapshot/review, 5032 health | Any open requirement or weak proof reopens owning subbundle. |

## Architecture invariants checked every phase

- Canonical stores/modules are the only write sources of truth.
- Snapshots are immutable and read-only; publication order, stable content identity, freshness/profile generation, and coverage are projection concepts and never canonical write-concurrency tokens.
- Live runtime resources are per execution.
- No shared `DbContext` parallelism.
- No untyped topics, cache keys, or phase parsing.
- Operational publication cannot change canonical execution success.
- Partition identity is sufficient for current module isolation and later authorized projection.
- UI remains a projection/orchestrator and uses existing component-library patterns.
