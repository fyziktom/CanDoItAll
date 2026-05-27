# SB18: 18-final-release-gate-and-real-test-runbook

## Goal

Final gate before user performs real tests.

## Required work

- Run full focused validation.
- Write a short release-readiness report.
- Create exact runbook for the next real live process test.
- Include abort criteria and expected diagnostics for each process step.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB18` are updated and downstream subbundles can rely on it.
