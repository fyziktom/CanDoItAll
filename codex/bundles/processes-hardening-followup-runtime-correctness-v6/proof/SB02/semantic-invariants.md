# SB02 Semantic Invariants

## Invariant SB02-INV-001

- Invariant ID: `SB02-INV-001`
- Source raw note: RN05 "fail to deduplicate artifacts because projection identity is not fully materialized".
- Expected behavior: `RecordArtifactAsync` must normalize projection lineage once, persist `ProjectionLineageJson` and `ProjectionIdentityHash` from the same normalized object, and dedupe by `(ProcessRunId, ProjectionIdentityHash)` before using bounded external reference keys.
- Disallowed shallow implementation: Dedupe by bounded display key only, serialize a different lineage instance than the hash source, ignore long display-key collision risk, or test only empty projection lineage.
- Failing-first test: N/A; current reviewed branch already had hash-based dedupe, so SB02 used red-team positive variation for long bounded display keys instead of manufacturing a revert-only failure.
- Passing test: `bundle://proof/SB02/transcripts/passing.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs`; `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- Production assertions: `RecordArtifactAsync` queries by `ProjectionIdentityHash` before `ExternalReferenceKey`; persisted lineage JSON uses `SerializeNormalized` on the normalized lineage object.
- Red-team negative case: The long-display-key test uses two different bounded display keys with the same lineage and proves only one artifact record is persisted.
- Downstream dependency check: SB03 and SB08 can rely on stable artifact identity for completion validation and storage-backed content checks.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Projection identity hash | `ProcessArtifactProjectionLineageJson.Normalize` | `ProcessesService.RecordArtifactAsync` | Projection lineage is normalized, hash is used for dedupe, and matching hash/JSON are persisted on the artifact record. | `bundle://proof/SB02/transcripts/passing.txt` |
