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
