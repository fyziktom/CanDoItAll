# SB09: 09-refactor-checkpoint-a-maf16-adapter-boundaries

## Goal

Refactor MAF adapter seams after adoption work.

## Required work

- Extract compatibility wrappers for MAF 1.6 APIs.
- Keep CanDoItAll runtime models independent of MAF internals.
- Document which MAF 1.6 features are adopted vs deferred.
- Run all MAF-focused tests before continuing.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB09` are updated and downstream subbundles can rely on the behavior.
