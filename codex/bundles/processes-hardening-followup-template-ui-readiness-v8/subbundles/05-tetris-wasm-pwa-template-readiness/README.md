# SB05: tetris-wasm-pwa-template-readiness

## Status

- Completed

## Objective

Prepare a reusable process template/profile for the planned Blazor WASM PWA Tetris UI run.

## Covered Inputs

- RQ04 Tetris WASM PWA readiness
- F02 Blazor template boundary dependency

## Prerequisites

- SB04 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://Templates/Processes/processes/blazor-app-delivery
- repo://Templates/Processes/seed-catalog/baseline-scenarios.json

## Scope

- Do not hardcode Tetris into the process runtime core.
- Add either a template variant, baseline scenario, or documented launch profile that configures `blazor-app-delivery` for a simple Tetris Blazor WASM PWA.
- Acceptance criteria must include playable Tetris board, falling tetrominoes, keyboard controls, scoring, line clear, game over, restart, pause/resume, PWA/offline readiness where feasible, browser screenshot, console proof, build/test proof, and project-structure writeback.
- Ensure the first step only resolves architecture/contract and cannot implement.
- Ensure the implementation step is responsible for code, and validation/QA cannot mutate code.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB05/.

## Implementation Steps

- Do not hardcode Tetris into the process runtime core.
- Add either a template variant, baseline scenario, or documented launch profile that configures `blazor-app-delivery` for a simple Tetris Blazor WASM PWA.
- Acceptance criteria must include playable Tetris board, falling tetrominoes, keyboard controls, scoring, line clear, game over, restart, pause/resume, PWA/offline readiness where feasible, browser screenshot, console proof, build/test proof, and project-structure writeback.
- Ensure the first step only resolves architecture/contract and cannot implement.
- Ensure the implementation step is responsible for code, and validation/QA cannot mutate code.

## Scope Exceptions

- None planned. Any discovered exception must be recorded as a blocker, reopened subbundle, or concrete follow-up before closure.

## Do Not Do

- Do not hardcode Tetris behavior into generic process runtime code.
- Do not introduce SQLite paths or non-PostgreSQL persistence assumptions.
- Do not replace runtime proof with source-text-only assertions for behavior-changing work.
- Do not silently narrow raw notes that say all, every, must, or same flow.

## Acceptance Checklist

- Required work is implemented or explicitly blocked with a follow-up.
- Targeted tests and relevant audit commands pass.
- bundle://proof/SB05/manifest.md and bundle://proof/SB05/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB05/manifest.md.
- Semantic invariant contract: bundle://proof/SB05/semantic-invariants.md.
- Command transcripts: bundle://proof/SB05/transcripts/.

## Browser Validation Logging

- Record route, viewport, Playwright MCP evidence, screenshot paths, console assertions, and result in `bundle://reviews/01-execution-report.md` when browser-visible proof is produced.

## Progression Gate

- Closure gate passed: proof artifacts exist under `bundle://proof/SB05/`, referenced paths resolve, and downstream dependency impact is recorded in `bundle://reviews/01-execution-report.md`.
- Dependent subbundles may rely on the reusable Tetris WASM PWA scenario without hardcoding sample-specific behavior into the runtime core.

## Suggested Agent Prompt

- Execute SB05 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB05` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
