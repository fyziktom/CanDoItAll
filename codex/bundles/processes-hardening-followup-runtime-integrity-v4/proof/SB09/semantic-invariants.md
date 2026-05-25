# SB09 Semantic Invariants

## SB09-INV-001

Expected behavior: no-progress retry fingerprints are durable across dispatcher restarts and active execution run adoption is limited to the current step attempt window.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Required proof:

- failing-first/red-team proof
- passing proof
- source assertions
- anti-stub audit
- changed-file hashes
- production behavior artifact matrix when new runtime state is introduced

Implemented proof:

- `NoProgressRetrySignal` records execution run id, tool signature, artifact validation fingerprint, mutation delta, proof delta, and the combined correlation fingerprint.
- Retry execution writes `no-progress-retry-observed` journal entries before queueing recovery, and later retry decisions consume prior observed/compressed journal entries for the same process run, step run, and fingerprint.
- Repeated no-progress fingerprints from a different execution run stop retry after restart; duplicate processing of the same execution run is not treated as a restart loop.
- Active execution run reconciliation excludes runs that started before the current step attempt window, even when those runs remain active and recently updated.
- Focused integration tests cover both durable restart detection and current-attempt active-run filtering.
