# SB16: 16-generic-process-regression-business-and-agent-training

## Goal

Protect generic process runtime.

## Required work

- Run non-software process templates through lint/import/start/read-model tests.
- Add or validate agent-training/improvement process template pattern.
- Ensure artifact validation and MAF adoption do not assume software/build/browser artifacts.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB16` are updated and downstream subbundles can rely on it.
