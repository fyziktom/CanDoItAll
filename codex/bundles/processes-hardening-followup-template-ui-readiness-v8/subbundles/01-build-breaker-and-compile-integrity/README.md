# SB01: 01-build-breaker-and-compile-integrity

## Goal

Fix build/compile integrity before all other work.

## Required work

- Run `dotnet build CanDoItAll.slnx --no-restore` before changing code and capture the failure.
- Verify whether `ProcessStepRecoveryOption.None` is missing or whether another source defines it.
- Either add `None = 0` to `ProcessStepRecoveryOption` or change read-model defaults to a valid non-action option. Prefer `None = 0` for API clarity.
- Audit all enums recently extended in phase7 for read-model defaults that reference non-existent members.
- Add a unit or compile-focused source assertion test so this cannot regress.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path test.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on whether this affects the planned Blazor WASM PWA/Tetris UI test.

## Closure criteria

This subbundle is not complete until the proof files under `proof/SB01` are updated and the next dependent subbundle can rely on the behavior without reinterpreting prose.
