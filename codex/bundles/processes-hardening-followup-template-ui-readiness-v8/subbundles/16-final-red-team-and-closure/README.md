# SB16: 16-final-red-team-and-closure

## Goal

Run final red-team closure across templates and runtime.

## Required work

- Run build and focused tests.
- Run template typed-operation contract audit over all templates.
- Run red-team tests: architect tries to implement, QA tries to mutate code, writeback step tries to mutate product source, API transition tries to complete with weak artifact, workflow output mapping ambiguous, subprocess output missing.
- Run PostgreSQL-only audit.
- Update proof manifests and final execution report.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB16` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
