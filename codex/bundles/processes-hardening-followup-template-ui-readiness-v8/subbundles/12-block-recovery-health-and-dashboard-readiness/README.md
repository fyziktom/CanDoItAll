# SB12: block-recovery-health-and-dashboard-readiness

## Status

- Completed

## Objective

Make block/recovery state reliable and observable.

## Covered Inputs

- RQ05 API/tool/skill parity
- RQ07 unified artifact validation

## Prerequisites

- SB11 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBlockStateClassifier.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs

## Scope

- Stop inferring typed block causes from prose in new runtime paths; carry `BlockCause` from finalizer/tool/API into transitions.
- Use text inference only as legacy fallback.
- Expose block reason code, recovery options, next recovery action, and invariant diagnostics consistently through run detail and health APIs.
- Add tests for own missing artifact vs upstream missing artifact classification.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB12/.

## Implementation Steps

- Stop inferring typed block causes from prose in new runtime paths; carry `BlockCause` from finalizer/tool/API into transitions.
- Use text inference only as legacy fallback.
- Expose block reason code, recovery options, next recovery action, and invariant diagnostics consistently through run detail and health APIs.
- Add tests for own missing artifact vs upstream missing artifact classification.

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
- bundle://proof/SB12/manifest.md and bundle://proof/SB12/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB12/manifest.md.
- Semantic invariant contract: bundle://proof/SB12/semantic-invariants.md.
- Command transcripts: bundle://proof/SB12/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB12 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB12` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.

## Closure Notes

- Closed with production changes in runtime block-state classification and recovery routing.
- Added SB12-named tests for typed-cause precedence, legacy fallback ownership, transition run-detail health, and HTTP upstream missing-artifact recovery health.
- Browser validation is not required for this backend/API health checkpoint; the planned Tetris browser/UI preflight remains assigned to SB15.
