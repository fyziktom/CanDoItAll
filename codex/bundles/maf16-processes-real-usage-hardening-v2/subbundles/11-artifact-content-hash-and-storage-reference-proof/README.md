# SB11: 11-artifact-content-hash-and-storage-reference-proof

## Goal

Close content hash/storage reference semantics.

## Required work

- Verify `RecordArtifactAsync` computes content hash for workspace and storage reference artifacts.
- Verify content hash is preserved through projection lineage JSON and projection identity hash.
- Verify empty content hash is not treated as success for required evidence when content is unreadable.
- Add tests for storage reference, organization-scoped path, and plain run-scoped path.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB11` are updated and downstream subbundles can rely on the behavior.
