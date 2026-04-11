# SQLite write-path hardening

## Purpose
Review the process-module database write paths from a SQLite-first perspective, remove risky multi-context patterns, and define the tests needed to catch locking or partial-write regressions.

## Depends on
04-architecture-review-gate-a

## Deliverables
- SQLite risk register for the process module
- Refactor plan for single-context or explicit-transaction write paths
- Integration tests for import metadata, repeated seeding, and write coordination

## Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.cs`
- `tests/CanDoItAll.Tests.Integration/SqliteWriteCoordinationIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessImportMetadataIntegrationTests.cs`

## Validation commands or checks
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~Sqlite`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessImportMetadataIntegrationTests`

## Senior review questions
- Does any process write path still rely on chained contexts or non-atomic follow-up updates?
- Could SQLite locking or a partial failure leave process metadata inconsistent?
- Are the tests strong enough to catch provider-specific regressions?

## Strict corrective rule
Create a SQLite corrective subbundle and do not continue until the write-path risk is reduced to an explicitly accepted level.
