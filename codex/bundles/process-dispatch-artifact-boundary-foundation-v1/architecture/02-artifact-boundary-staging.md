# Artifact Boundary Staging

## Stage 1: Inventory And Design

List every method in artifact/projection/validation partials and classify it as one of:

- Expectation matching.
- Projection source discovery.
- Projection lineage/key generation.
- Storage placement/DB recording.
- Required-artifact validation.
- Browser/quality/runtime proof validation.
- Recovery artifact handling.
- Completion/finalization transition logic.
- Cross-cutting utility/noise token logic.

## Stage 2: Pure Helpers

Extract helper logic that can be unit tested without DB/storage:

- Matching expected artifacts to execution artifact metadata/content.
- Building stable external-reference keys and lineage snapshots.
- Classifying candidate artifact kind/trust status/sensitivity defaults.
- Validating response evidence text for selected high-risk rules.

## Stage 3: Projection Planner

Create projection candidate records that describe what should be projected but do not yet place storage or record DB rows.

## Stage 4: First Migration

Migrate only execution artifact projection through the planner. Do not migrate all projection sources at once.

## Stage 5: Additional Adapters

Add planning adapters for response/mock/workspace source categories. Migrate only when parity tests are strong.

## Stage 6: Validation Service Foundation

Extract selected validation rules into a rule service. Avoid moving step transition/finalization state logic.
