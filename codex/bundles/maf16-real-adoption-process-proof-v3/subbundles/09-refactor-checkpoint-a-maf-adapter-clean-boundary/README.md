# SB09: 09-refactor-checkpoint-a-maf-adapter-clean-boundary

## Goal

Refactor MAF adapter after feature-adoption audit.

## Required work

- Move MAF 1.6 compatibility/adoption helpers into a small boundary layer.
- Keep Processes and domain runtime models independent from MAF internals.
- Update docs: package version policy, adopted features, deferred features, and upgrade watch for MAF 1.7.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB09` are updated and downstream subbundles can rely on it.
