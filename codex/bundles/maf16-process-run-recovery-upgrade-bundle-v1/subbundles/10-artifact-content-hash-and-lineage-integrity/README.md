# SB10: 10-artifact-content-hash-and-lineage-integrity

## Goal

Fix content hash and content-backed validation semantics.

## Required work

- Compute content hash for workspace-written managed artifacts where possible.
- If content cannot be read, classify as `ContentUnavailable` or equivalent, not `StaleOrWrongRun`.
- Ensure `ProjectionIdentityHash` remains stable and unique after content hash addition.
- Ensure content hash is not required for legitimate non-file manual decision artifacts unless validation contract requires it.
- Add tests for empty content hash, unreadable managed file, and content mismatch.

## Required proof

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB10` are updated and the next subbundle can safely depend on it.
