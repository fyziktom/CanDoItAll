# Assumptions And Risks

## Assumptions

- Existing source adapters preserve source semantics and external-reference keys.
- `ProcessArtifactProjectionWriteCoordinator` is the right place for storage-backed write side effects.
- Completed-decision artifacts should remain record-only and must not be forced through storage placement.
- Dispatcher state updates such as `candidate.ExternalReferenceKeys.Add(...)` and `candidate.RecordedArtifactExpectationIds.Add(...)` may remain dispatcher-owned unless this bundle proves a safe narrower result helper.

## Critical Path Risks

- Migrating multiple write paths at once can hide a key/provenance/trust-status regression.
- Response-text projection writes a file before placing it into managed storage; this path needs special care to avoid changing file contents or path safety behavior.
- Provider-native browser artifacts have both expected-output and discovered-output modes; these must not be collapsed into one generic path.
- Process mock artifacts currently throw on some failures; coordinator result semantics must not accidentally convert required mock failures into soft warnings.

## Validation Risks

- Compile-only proof is insufficient.
- Count-only line reduction is insufficient.
- Tests must prove key parity, lineage parity, managed storage path behavior, candidate state update behavior, and failure semantics.

## Reopen Triggers

- Any external reference key format changes without an explicit intentional migration and compatibility plan.
- Any artifact expectation is recorded as satisfied without matching the old required-artifact path.
- Any Process Core or driver-pack project appears.
- Any small/medium/mobile proof artifact appears.
