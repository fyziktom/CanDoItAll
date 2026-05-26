# SB16: 16-final-red-team-closure-and-live-test-runbook

## Goal

Close the bundle with red-team proof and a live UI test runbook.

## Work items

- Run build and focused tests.
- Run red-team checks: seeded baseline mistaken for live run, architect mutates code, QA mutates code, writeback edits product files, missing screenshot, missing console proof, missing project-structure writeback, missing role tool.
- Produce a concrete next-step runbook for the user: what to click, what to verify after each process step, and expected artifacts.
- Update final execution report and proof manifests.

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
