# Process schema referential integrity hardening

## Purpose

Add the missing definition-child and runtime foreign keys, choose delete behaviors intentionally, and stop relying on ordered delete code as the primary integrity mechanism.

## Required deliverables
- Explicit FK mappings for the remaining Process definition-child and runtime tables.
- Provider migrations for SQLite and PostgreSQL reflecting the new FK graph.
- A short written delete-behavior map explaining which links cascade, restrict, or set-null and why.
- Integration tests proving the DB rejects representative orphan rows and invalid references.

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/ProcessDefinitionEntities.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Publication.cs`
- `src/CanDoItAll.Migrations.Sqlite/Migrations`
- `src/CanDoItAll.Migrations.PostgreSql/Migrations`
- `tests/CanDoItAll.Tests.Integration`

## Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessSchema|FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --startup-project src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj --context AppDbContext`
- `dotnet ef migrations script --project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --context AppDbContext`

## Review questions
1. Does the DB now reject representative orphan definition-child and runtime rows?
2. Are delete behaviors explicit and documented instead of incidental?
3. Did FK hardening preserve differential save and delete behavior?

## Corrective trigger

If FK hardening causes save-cycle failures, do not quietly remove the FK plan. Open the DB-integrity corrective playbook and redesign the save ordering or ownership model first.

## Corrective template

- `subbundles/_corrective-db-integrity-reset`

## Detailed execution notes

Use the following default decision rule unless a better documented reason exists:

- aggregate-owned children: `Cascade`
- cross-edge references to peer aggregates/peer rows: `Restrict`
- optional historical links where deletion should preserve history: `SetNull`

At minimum, review FK coverage for:
- `ProcessStepDependencyDefinition`
- `ProcessStepBranchOutcomeDefinition`
- `ProcessStepRoleAssignmentRequirement`
- `ProcessArtifactExpectation`
- `ProcessStepArtifactInputDefinition`
- `ProcessRun`
- `ProcessStepRun`
- `ProcessRunAssignment`
- `ProcessWorkBrief`
- `ProcessDecisionRecord`
- `ProcessArtifactRecord`
- `ProcessJournalEntry`
- `ProcessConformanceObservation`
- `ProcessImprovementCandidate`
