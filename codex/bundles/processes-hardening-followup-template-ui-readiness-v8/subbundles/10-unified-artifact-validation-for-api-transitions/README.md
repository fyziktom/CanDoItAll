# SB10: unified-artifact-validation-for-api-transitions

## Status

- Completed

## Objective

Prevent manual/API transition from being weaker than automation finalization.

## Covered Inputs

- RQ07 unified artifact validation
- F06 manual/API transition validation weakness

## Prerequisites

- SB09 closure gate is Completed or honestly Blocked with an explicit follow-up.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs

## Scope

- Extract finalizer-grade artifact validation into a shared service.
- Use it from automation finalizer and `TransitionStepAsync` when completing a step through API/manual routes.
- Ensure transition validation checks content, lineage, producer kind, current-run binding, placeholder/gap markers, and managed evidence.
- Keep exception/disposition branch behavior explicit and typed.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB10/.

## Implementation Steps

- Extract finalizer-grade artifact validation into a shared service.
- Use it from automation finalizer and `TransitionStepAsync` when completing a step through API/manual routes.
- Ensure transition validation checks content, lineage, producer kind, current-run binding, placeholder/gap markers, and managed evidence.
- Keep exception/disposition branch behavior explicit and typed.

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
- bundle://proof/SB10/manifest.md and bundle://proof/SB10/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB10/manifest.md.
- Semantic invariant contract: bundle://proof/SB10/semantic-invariants.md.
- Command transcripts: bundle://proof/SB10/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passes only after proof artifacts exist, referenced paths resolve, and downstream dependency impact is recorded.
- Dependent subbundle may start only after the closure gate is Completed or the blocker is explicit.

## Suggested Agent Prompt

- Execute SB10 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

Closed with adversarial manual-transition proof and shared-validator source assertions in `bundle://proof/SB10/`. SB11/SB12 may rely on manual/API completion no longer bypassing finalizer-grade artifact validation.
