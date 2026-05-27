# SB14: 14-live-blazor-tetris-run-preflight

## Goal

Prepare a real live process rerun after the fixes.

## Required work

- Use live-run profile, not seeded baseline transitions/artifacts.
- Verify assignments, skills, tools, and operation contracts before dispatch.
- Run step 0 only in a smoke mode and verify current-run delivery contract artifact validates.
- Do not proceed to full implementation until step 0 proof is stable.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB14` are updated and downstream subbundles can rely on the behavior.
