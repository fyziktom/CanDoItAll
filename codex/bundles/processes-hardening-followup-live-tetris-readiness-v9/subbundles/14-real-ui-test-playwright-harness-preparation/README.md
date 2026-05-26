# SB14: 14-real-ui-test-playwright-harness-preparation

## Goal

Prepare the real UI test harness without executing the final user test yet.

## Work items

- Create a Playwright test or manual runbook that imports/selects the live Tetris profile, starts a run, inspects step boundaries, assigns agents, observes progress, and validates artifacts.
- The harness should be able to stop after first step to prove architecture-only behavior.
- The harness should be able to continue to implementation and QA once agents/tools are configured.
- Record expected evidence screenshots, URLs, console proof, and artifacts.

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
