# SB12: 12-runtime-health-debuggability-for-live-run

## Goal

Make a failed live test debuggable from UI/API.

## Work items

- Ensure run detail exposes step status, block reason code, block cause, recovery options, next action, missing artifact count, invariant diagnostics, latest execution attempts, and relevant artifact satisfaction.
- Add a compact UI panel or API field for 'why this process is blocked and what to do next'.
- Add tests for OwnOutput vs UpstreamInput classification.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- A note explaining how this improves readiness for the real UI-driven Blazor WASM PWA Tetris test.
- A note explaining how generic process behavior remains protected.

## Closure criteria

This subbundle is complete only when its proof manifest is updated and the next subbundle can rely on the result.
