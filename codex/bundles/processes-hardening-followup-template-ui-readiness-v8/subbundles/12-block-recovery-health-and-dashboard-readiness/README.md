# SB12: 12-block-recovery-health-and-dashboard-readiness

## Goal

Make block/recovery state reliable and observable.

## Required work

- Stop inferring typed block causes from prose in new runtime paths; carry `BlockCause` from finalizer/tool/API into transitions.
- Use text inference only as legacy fallback.
- Expose block reason code, recovery options, next recovery action, and invariant diagnostics consistently through run detail and health APIs.
- Add tests for own missing artifact vs upstream missing artifact classification.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB12` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
