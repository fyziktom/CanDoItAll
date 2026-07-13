# 04-database-migration-and-test-isolation

## Status

- `Completed`

## Objective

Resolve the historical database migration/test failure by proving whether it is a real pending migration or test-order/static-state contamination.

## Covered Inputs

- RH-007: historical `PendingModelChangesWarning` in broad unit run.
- RH-008: current EF pending-model proof is clean.

## Prerequisites

- SB02 should be resolved before final DB proof if build/watch path fixes changed runtime build behavior.
- Evidence exists: `bundle://evidence/database-runtime-switch-test.txt` and `bundle://evidence/ef-pending-model-check.txt`.

## Exact Source References

- `repo://tests/Unit/CanDoItAll.Tests.Unit/DatabaseRuntimeSwitchingTests.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/PostgreSqlAppDbContextFactory.cs`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AppDbContextModelRegistryTests.cs`

## Deliverables

- A clear decision: no migration needed, migration needed, or test isolation bug fixed.
- If no migration is needed, tests must isolate static `AppDbContextModelRegistry` state or prove it cannot leak.
- If a migration is needed, include EF pending-model failure proof before generating it.

## Dependency Impact

- SB05 full-suite proof depends on stable DB setup. A false-positive migration decision can destabilize runtime and integration tests.

## Validation Depth

- Critical database validation foundation.

## Implementation Steps

1. Run isolated DB runtime-switch test and EF pending-model check as current proof.
2. Try an order-specific reproduction around tests that call `AppDbContextModelRegistry.ConfigureAssemblies`.
3. If static registry leakage is found, add a test-only reset/scope mechanism or isolate tests without changing runtime semantics.
4. If EF pending-model check fails after isolation, generate/review the minimal migration.
5. Rerun isolated DB and pending-model proof.

## Scope Exceptions

- Do not fix unrelated integration database failures unless they reproduce this same pending-model/isolation issue.

## Do Not Do

- Do not generate a migration while `has-pending-model-changes` is clean.
- Do not suppress `PendingModelChangesWarning` globally to make tests pass.

## Acceptance Checklist

- [x] `AppDbContextRuntimeSwitchTests` passes in isolation.
- [x] An order-specific DB proof is recorded.
- [x] EF pending-model check is clean after restoring the cognitive-memory module snapshot migration.
- [x] No global suppression hides pending model changes.

## Proof Required

- Transcript: `proof/SB04/database-runtime-switch-test.txt`.
- Transcript: `proof/SB04/ef-pending-model-check.txt`.
- If applicable, order-reproduction transcript and passing order-recheck transcript.
- Source assertion for any static registry reset/isolation mechanism.

## Browser Validation Logging

- N/A. Database/test-only.

## Progression Gate

- SB05 may proceed only after EF migration state is explicitly clean or a reviewed migration is present and tests pass.

## Suggested Agent Prompt

```text
Implement SB04 only. Prove pending migration versus test isolation before editing; do not add a migration without failing EF pending-model evidence.
```
