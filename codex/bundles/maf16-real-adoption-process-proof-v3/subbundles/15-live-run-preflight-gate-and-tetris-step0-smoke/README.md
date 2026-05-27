# SB15: 15-live-run-preflight-gate-and-tetris-step0-smoke

## Goal

Prepare a safe real-run gate before full live test.

## Required work

- Run only step 0 through live profile or deterministic real-ish harness.
- Verify current-run delivery contract artifact validates through finalizer and read model.
- Do not proceed to implementation until this gate passes.
- Capture API evidence bundle for the step0 smoke.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB15` are updated and downstream subbundles can rely on it.
