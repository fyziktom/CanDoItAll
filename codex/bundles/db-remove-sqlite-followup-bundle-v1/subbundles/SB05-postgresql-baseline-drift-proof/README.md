# SB05 - PostgreSQL baseline drift proof

## Goal

Prove the single PostgreSQL baseline migration is valid, complete, and not drifting from the current EF model.

## Context

The branch consolidated PostgreSQL migrations into `20260523211921_InitialPostgreSqlBaseline`, but this must be proven against the current model.

## Required changes

1. Verify only one baseline migration exists under `src/CanDoItAll.Migrations.PostgreSql/Migrations`.
2. Create a fresh PostgreSQL test database and apply migrations from zero.
3. Run all module schema initializers and ensure they do not create missing baseline objects that should be in migration.
4. Run EF drift check:
   - create temporary migration `__PostgreSqlModelDriftCheck`,
   - verify it is empty/no meaningful changes,
   - delete it before final commit.
5. Generate SQL script from baseline and store proof under evidence.
6. Update manual real DB alignment guidance.

## Validation

- `dotnet ef migrations list` shows only baseline.
- `dotnet ef database update` against a fresh DB passes.
- Drift check is empty or documented with required fixes implemented.
- Integration tests pass after fresh DB creation.

## Proof artifacts

Write:

- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`
- relevant logs under `evidence/SB05/`

## Acceptance criteria

- Fresh PostgreSQL database can be created from baseline.
- No EF model drift remains.
- Manual real DB alignment document is clear and not over-automated.

## Status

- Completed

## Objective

Prove the PostgreSQL baseline migration is the only migration and has no EF model drift.

## Covered Inputs

- `bundle://requirements/01-followup-requirements.md`

## Prerequisites

- SB04 residue/test hardening completed.

## Exact Source References

- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs`
- `bundle://evidence/SB05/postgresql-baseline-idempotent.sql`

## Deliverables

- Migration file inventory.
- EF migrations list proof.
- Idempotent baseline SQL script.
- Empty temporary drift migration verification.

## Dependency Impact

- Runtime startup and integration tests rely on the baseline to create fresh PostgreSQL schema.

## Validation Depth

- EF tooling, migration bootstrap integration tests, and generated SQL proof.

## Implementation Steps

- List migration files.
- Run migration bootstrap integration tests.
- Add and inspect temporary drift migration, then remove it.
- Generate idempotent SQL.

## Do Not Do

- Do not leave temporary drift migrations in the working tree.

## Acceptance Checklist

- Only baseline migration and snapshot remain.
- EF drift migration is empty and removed.
- Idempotent SQL evidence is generated.

## Proof Required

- `bundle://proof/SB05/manifest.md`

## Browser Validation Logging

- No browser route is required for this subbundle.

## Progression Gate

- Baseline proof must pass before runtime tuning and final validation.

## Suggested Agent Prompt

Implement SB05, then run the EF and migration proof commands in `proof/SB05/manifest.md`.
