# Source Adapter Boundary

Source adapters should isolate the repeated logic currently embedded in `ArtifactProjection.cs`:

- source-specific external reference key construction
- duplicate-skip key computation
- expectation matching input assembly
- title/review/provenance default inputs
- projection source kind assignment
- source path normalization

They should return typed projection plans or skipped-source diagnostics. They should not perform storage or DB writes.

## First Adapter Set

1. Process mock artifact adapter.
2. Workspace-written artifact adapter.
3. Existing-managed artifact adapter.
4. Assistant-response artifact adapter.
5. Provider-native browser artifact adapter.

Each adapter migration must include exact before/after key parity tests.
