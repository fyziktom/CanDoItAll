# SB05: 05-tetris-wasm-pwa-template-readiness

## Goal

Prepare a reusable process template/profile for the planned Blazor WASM PWA Tetris UI run.

## Required work

- Do not hardcode Tetris into the process runtime core.
- Add either a template variant, baseline scenario, or documented launch profile that configures `blazor-app-delivery` for a simple Tetris Blazor WASM PWA.
- Acceptance criteria must include playable Tetris board, falling tetrominoes, keyboard controls, scoring, line clear, game over, restart, pause/resume, PWA/offline readiness where feasible, browser screenshot, console proof, build/test proof, and project-structure writeback.
- Ensure the first step only resolves architecture/contract and cannot implement.
- Ensure the implementation step is responsible for code, and validation/QA cannot mutate code.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB05` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
