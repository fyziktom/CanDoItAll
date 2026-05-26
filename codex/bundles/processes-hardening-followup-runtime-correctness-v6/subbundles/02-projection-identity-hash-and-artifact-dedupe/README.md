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

## Status

- Completed

## Covered Inputs

- RN05 fail to deduplicate artifacts because projection identity is not fully materialized.
- RQ02 projection identity hash.

## Prerequisites

- Prepared-stage bundle validator passes after structural repair.
- No production artifact identity changes from SB01 are required.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Deliverables

- Projection lineage normalized once per artifact record operation.
- Projection identity hash persisted from the same normalized lineage.
- Deduplication by `(ProcessRunId, ProjectionIdentityHash)` before display-key fallback.

## Dependency Impact

- SB03 and SB08 rely on stable artifact identity for validation and retry behavior.
- SB14 red-team closure must prove duplicate projection retries do not create duplicate artifacts.

## Validation Depth

- Integration tests with long external reference keys and same normalized lineage.
- Recovery retry test or equivalent projection retry smoke.
- Source assertions for identity hash persistence and fallback ordering.

## Implementation Steps

- Add failing-first coverage for bounded display-key collision or lost identity.
- Normalize lineage once in `RecordArtifactAsync`.
- Query existing records by projection identity hash before external reference fallback.
- Persist normalized lineage JSON and hash together.
- Record proof under `bundle://proof/SB02/`.

## Do Not Do

- Do not use bounded external reference keys as the identity source.
- Do not add provider-switching or SQLite-specific migration logic.
- Do not serialize a hash from a different lineage instance than the stored JSON.

## Acceptance Checklist

- Duplicate normalized lineage returns the existing artifact record.
- Long or colliding display keys do not drive identity.
- Persisted JSON contains the identity hash produced from normalized lineage.
- Focused integration tests pass.

## Proof Required

- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- Failing-first or red-team transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB02 changes persistence/runtime identity only.

## Progression Gate

- Passed. SB03 may rely on stable artifact projection identity, normalized persisted lineage, and identity-hash dedupe before external reference fallback.

## Suggested Agent Prompt

- Implement SB02 exactly as scoped, keep external reference keys display-only, update `proof/SB02`, run focused integration tests, and record gate results.
