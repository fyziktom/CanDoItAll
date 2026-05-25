# SB02: Persist projection identity hash and dedupe by lineage identity.

## Objective

Persist projection identity hash and dedupe by lineage identity.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Normalize projection lineage in `RecordArtifactAsync` once.
- Persist both `ProjectionLineageJson` and `ProjectionIdentityHash` from the normalized lineage.
- Deduplicate existing artifacts by `(ProcessRunId, ProjectionIdentityHash)` before using bounded external reference keys.
- Add tests for long lineage/external reference where bounded external reference key would collide or lose identity.
- Ensure recovery retry projects one artifact record, not duplicates.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
