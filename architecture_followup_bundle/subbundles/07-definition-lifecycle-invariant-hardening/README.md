# Definition lifecycle invariant hardening

## Purpose

Enforce the lifecycle rules that the Process services already assume: single draft, single published version, valid active published binding, and conflict-safe version allocation.

## Required deliverables
- Schema-level enforcement for one draft per definition.
- Schema-level enforcement for one published version per definition.
- A safe binding strategy for `ActivePublishedVersionId`, preferably including same-definition enforcement.
- A conflict-safe version allocator that does not use `MAX + 1`.
- Retry-aware or otherwise hardened slug allocation semantics under uniqueness conflicts.

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Migrations.Sqlite/Migrations`
- `src/CanDoItAll.Migrations.PostgreSql/Migrations`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessSchema" -v:minimal`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --startup-project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --context AppDbContext`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext`

## Review questions
1. Can the DB now enforce one draft and one published version per definition?
2. Can `ActivePublishedVersionId` no longer point to an invalid or foreign version without being rejected?
3. Did version allocation stop relying on `MAX + 1`?

## Corrective trigger

If lifecycle singularity still exists only in ordering logic or if `MAX + 1` remains, fail and open the lifecycle corrective playbook before any closure work continues.

## Corrective template

- `subbundles/_corrective-lifecycle-reset`

## Detailed execution notes

Preferred lifecycle hardening moves:

- add filtered unique index for one draft per definition;
- add filtered unique index for one published version per definition;
- protect `ActivePublishedVersionId` with at least a foreign key, and prefer same-definition enforcement if feasible;
- replace the version allocator with a definition-owned counter or another equally strong transaction-safe strategy;
- make slug allocation resilient to uniqueness races instead of relying only on a pre-check loop.
