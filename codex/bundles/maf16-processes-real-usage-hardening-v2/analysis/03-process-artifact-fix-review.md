# Process Artifact Fix Review

## Good signs

- The previous failed state, where a current-run workspace-written artifact was rejected as `StaleOrWrongRun`, now has targeted code paths:
  - `ProcessCompletionArtifactValidator`
  - `StorageBackedProcessArtifactContentReader`
  - `TryValidateManagedArtifactContent`
  - `RecordArtifactAsync` content-hash computation
  - current-run validation with lineage

## Follow-up risk areas

- `RecordArtifactAsync` returns an existing artifact by `ProjectionIdentityHash` or `ExternalReferenceKey`. Verify this cannot return an artifact for the wrong step/expectation within the same run.
- If content hash computation fails, the code can still store a record with empty content hash. That is acceptable only if final validation reports `ContentUnavailable` or equivalent when content is required.
- Ensure `contentHash` is not silently optional for evidence/runtime proof artifacts that require stored content.
- The read model and finalizer must agree on artifact status. Do not show `Satisfied` in step detail if finalizer would reject as content unavailable, wrong run, wrong producer mode, or stale.
- Manager recovery must not record an operator decision artifact and treat it as if it satisfied the original required artifact.
