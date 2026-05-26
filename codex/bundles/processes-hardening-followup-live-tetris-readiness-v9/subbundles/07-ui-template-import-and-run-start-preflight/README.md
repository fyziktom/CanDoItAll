# SB07: 07-ui-template-import-and-run-start-preflight

## Goal

Prepare the actual UI flow for import/select/start/observe process run.

## Work items

- Add a UI/API preflight route or documented UI sequence for selecting the live Tetris profile.
- Ensure template detail UI shows allowed operations, target scope, artifacts, branch outcomes, and baseline/live distinction.
- Ensure run start UI can pass scenario acceptance criteria into trigger reason or launch context without losing data.
- Add component or Playwright preflight tests for template selection and run creation.

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
