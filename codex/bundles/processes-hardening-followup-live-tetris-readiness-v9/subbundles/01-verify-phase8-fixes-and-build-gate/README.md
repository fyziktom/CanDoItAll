# SB01: 01-verify-phase8-fixes-and-build-gate

## Goal

Verify phase8 really fixed the previous structural issues and does not introduce build/test breakage.

## Work items

- Run `dotnet build CanDoItAll.slnx --no-restore` first and capture output.
- Assert `ProcessStepRecoveryOption.None` exists and all read-model defaults compile.
- Assert project-structure tools are registered/classified and unknown project_structure_* tools fail closed.
- Assert `blazor-app-delivery` revalidation/writeback/escalation steps no longer mutate product source.
- Update proof before continuing.

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
