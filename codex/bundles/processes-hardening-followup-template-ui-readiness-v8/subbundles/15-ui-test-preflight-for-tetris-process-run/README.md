# SB15: ui-test-preflight-for-tetris-process-run

## Status

- Completed

## Objective

Prepare the next UI test without running it yet.

## Covered Inputs

- RQ03 Blazor boundary correctness
- RQ04 Tetris WASM PWA readiness

## Prerequisites

- SB14 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunStepsDialog.razor

## Scope

- Create a preflight checklist for the upcoming UI test: start web app, import template, start Tetris run, inspect steps, ensure first step cannot mutate, let agents run, inspect artifacts, branch outcomes, and evidence.
- Add Playwright/component test hooks if missing.
- Ensure the API and UI expose enough data to debug process blockages during the Tetris run.
- Document expected screenshots, console proof, app URL, and artifact paths.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB15/.

## Implementation Steps

- Create a preflight checklist for the upcoming UI test: start web app, import template, start Tetris run, inspect steps, ensure first step cannot mutate, let agents run, inspect artifacts, branch outcomes, and evidence.
- Add Playwright/component test hooks if missing.
- Ensure the API and UI expose enough data to debug process blockages during the Tetris run.
- Document expected screenshots, console proof, app URL, and artifact paths.

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
- bundle://proof/SB15/manifest.md and bundle://proof/SB15/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB15/manifest.md.
- Semantic invariant contract: bundle://proof/SB15/semantic-invariants.md.
- Command transcripts: bundle://proof/SB15/transcripts/.

## Browser Validation Logging

- Record route, viewport, Playwright MCP evidence, screenshot paths, console assertions, and result in `bundle://reviews/01-execution-report.md` when browser-visible proof is produced.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB15 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB15` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

## Closure Notes

- Added runtime step dialog test hooks for step ids, operation target scope, allowed operations, branch selectors, block reason code, and recovery options.
- Added a component regression that renders the production process workspace dialog with a strict Tetris-like process and proves the first step cannot mutate product files.
- Added the Tetris browser-run preflight checklist under `bundle://proof/SB15/tetris-ui-preflight-checklist.md`.
- Actual browser execution remains deferred by this subbundle's scope.
