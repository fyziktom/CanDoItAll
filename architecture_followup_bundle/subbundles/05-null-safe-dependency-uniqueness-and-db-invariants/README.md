# Null-safe dependency uniqueness and DB invariants

## Purpose

Fix the dependency uniqueness hole caused by nullable branch-outcome keys and prove the invariant directly at the database level.

## Required deliverables
- A dependency uniqueness strategy that works for both unconditional and conditional routes.
- Provider migrations that enforce the chosen invariant correctly.
- Direct integration tests proving duplicate unconditional and conditional dependencies are rejected by the DB.
- Friendly module-level error translation where the DB raises uniqueness conflicts.

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Migrations.Sqlite/Migrations`
- `src/CanDoItAll.Migrations.PostgreSql/Migrations`
- `tests/CanDoItAll.Tests.Integration`

## Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessSchema|FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --startup-project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --context AppDbContext`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext`

## Review questions
1. Can the DB now reject duplicate unconditional dependencies where `DependsOnBranchOutcomeId` is null?
2. Can the DB still reject duplicate conditional dependencies for the same branch outcome?
3. Did the error surface stay understandable at the service boundary?

## Corrective trigger

If the solution still depends on the old nullable composite unique index alone, fail the gate and use the DB-integrity corrective playbook.

## Corrective template

- `subbundles/_corrective-db-integrity-reset`

## Detailed execution notes

Preferred implementation options:

1. Split filtered unique indexes:
   - unique `(StepDefinitionId, DependsOnStepId)` where `DependsOnBranchOutcomeId IS NULL`
   - unique `(StepDefinitionId, DependsOnStepId, DependsOnBranchOutcomeId)` where `DependsOnBranchOutcomeId IS NOT NULL`

2. Or a normalized non-null route key / normalized route-id column.

Do not keep the current nullable triple unique index as the only guard.
