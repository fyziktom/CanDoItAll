# SB02: 02-live-tetris-run-profile-not-seeded-baseline

## Goal

Create a clear live-run profile for Tetris separate from seeded baseline scenarios.

## Work items

- Keep `baseline-blazor-wasm-pwa-tetris` as regression/demo data.
- Add a separate live-run profile/runbook/template projection option that contains acceptance criteria and assignments but no pre-completed transitions or fake artifacts.
- Make the UI/API distinguish seeded baseline replay from live process start.
- Add a test proving the live profile starts steps as Pending/Ready, not Completed.
- Document the difference in the Processes API skill and template docs.

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
