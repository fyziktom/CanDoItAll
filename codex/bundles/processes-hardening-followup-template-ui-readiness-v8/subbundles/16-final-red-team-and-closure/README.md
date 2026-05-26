# SB16: final-red-team-and-closure

## Status

- Completed

## Objective

Run final red-team closure across templates and runtime.

## Covered Inputs

- RQ09 PostgreSQL-only generic core
- RQ10 red-team closure

## Prerequisites

- SB15 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://CanDoItAll.slnx
- repo://Templates/Processes
- repo://codex/bundles/processes-hardening-followup-template-ui-readiness-v8/reviews/01-execution-report.md

## Scope

- Run build and focused tests.
- Run template typed-operation contract audit over all templates.
- Run red-team tests: architect tries to implement, QA tries to mutate code, writeback step tries to mutate product source, API transition tries to complete with weak artifact, workflow output mapping ambiguous, subprocess output missing.
- Run PostgreSQL-only audit.
- Update proof manifests and final execution report.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB16/.

## Implementation Steps

- Run build and focused tests.
- Run template typed-operation contract audit over all templates.
- Run red-team tests: architect tries to implement, QA tries to mutate code, writeback step tries to mutate product source, API transition tries to complete with weak artifact, workflow output mapping ambiguous, subprocess output missing.
- Run PostgreSQL-only audit.
- Update proof manifests and final execution report.

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
- bundle://proof/SB16/manifest.md and bundle://proof/SB16/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB16/manifest.md.
- Semantic invariant contract: bundle://proof/SB16/semantic-invariants.md.
- Command transcripts: bundle://proof/SB16/transcripts/.

## Browser Validation Logging

- Record route, viewport, Playwright MCP evidence, screenshot paths, console assertions, and result in `bundle://reviews/01-execution-report.md` when browser-visible proof is produced.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB16 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

Closed with build/test/audit proof in `bundle://proof/SB16`, final execution-report updates, and completed-stage bundle validation.
