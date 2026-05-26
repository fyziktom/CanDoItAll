# SB01: build-breaker-and-compile-integrity

## Status

- Completed

## Objective

Fix build/compile integrity before all other work.

## Covered Inputs

- RQ01 compile/build integrity
- F01 potential missing ProcessStepRecoveryOption.None

## Prerequisites

- None; this is the first execution gate.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs

## Scope

- Run `dotnet build CanDoItAll.slnx --no-restore` before changing code and capture the failure.
- Verify whether `ProcessStepRecoveryOption.None` is missing or whether another source defines it.
- Either add `None = 0` to `ProcessStepRecoveryOption` or change read-model defaults to a valid non-action option. Prefer `None = 0` for API clarity.
- Audit all enums recently extended in phase7 for read-model defaults that reference non-existent members.
- Add a unit or compile-focused source assertion test so this cannot regress.

## Dependency Impact

- Downstream subbundles cannot rely on this phase until the closure gate records proof in bundle://reviews/01-execution-report.md.
- Critical-foundation behavior must be reopened if later proof contradicts the stated invariant.

## Validation Depth

- Entry gate with current source references before editing.
- Failing-first or adversarial proof where behavior changes.
- Passing production-path test or build proof.
- Source assertions, changed-file hashes, anti-stub audit, and proof manifest under bundle://proof/SB01/.

## Implementation Steps

- Run `dotnet build CanDoItAll.slnx --no-restore` before changing code and capture the failure.
- Verify whether `ProcessStepRecoveryOption.None` is missing or whether another source defines it.
- Either add `None = 0` to `ProcessStepRecoveryOption` or change read-model defaults to a valid non-action option. Prefer `None = 0` for API clarity.
- Audit all enums recently extended in phase7 for read-model defaults that reference non-existent members.
- Add a unit or compile-focused source assertion test so this cannot regress.

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
- bundle://proof/SB01/manifest.md and bundle://proof/SB01/semantic-invariants.md are updated when this subbundle changes behavior.
- bundle://reviews/01-execution-report.md contains the subbundle gate row and raw-note closure evidence.

## Proof Required

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.
- Proof manifest: bundle://proof/SB01/manifest.md.
- Semantic invariant contract: bundle://proof/SB01/semantic-invariants.md.
- Command transcripts: bundle://proof/SB01/transcripts/.

## Browser Validation Logging

- N/A for direct browser rendering unless implementation changes browser-visible behavior; still record the N/A decision in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Closure gate passed on 2026-05-26 with `bundle://proof/SB01/manifest.md`, `bundle://proof/SB01/semantic-invariants.md`, targeted integration proof, source assertions, anti-stub audit, and changed-file hashes.
- SB02 may rely on the recovery enum/read-model default contract without revalidating the stale compile-breaker finding.

## Suggested Agent Prompt

- Execute SB01 exactly as scoped here. Preserve the generic Processes runtime boundary, add minimal production changes and tests, update proof artifacts, and rerun the relevant validation commands before closing.

## Original Closure Criteria

This subbundle is not complete until the proof files under `proof/SB01` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
