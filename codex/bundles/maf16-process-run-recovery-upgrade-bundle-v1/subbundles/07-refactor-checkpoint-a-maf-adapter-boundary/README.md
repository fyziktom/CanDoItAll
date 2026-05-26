# SB07: 07-refactor-checkpoint-a-maf-adapter-boundary

## Goal

Stabilize the MAF adapter after upgrade.

## Required work

- Extract a small MAF adapter seam if compile fixes spread across many files.
- Keep CanDoItAll runtime models independent of MAF package internals.
- Consolidate MAF 1.6 compatibility helpers and source assertions.
- Run full MAF-focused tests before continuing to process runtime fixes.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB07` are updated and the next subbundle can safely depend on it.
