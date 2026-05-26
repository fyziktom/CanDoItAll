# SB15: 15-ui-test-preflight-for-tetris-process-run

## Goal

Prepare the next UI test without running it yet.

## Required work

- Create a preflight checklist for the upcoming UI test: start web app, import template, start Tetris run, inspect steps, ensure first step cannot mutate, let agents run, inspect artifacts, branch outcomes, and evidence.
- Add Playwright/component test hooks if missing.
- Ensure the API and UI expose enough data to debug process blockages during the Tetris run.
- Document expected screenshots, console proof, app URL, and artifact paths.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB15` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
