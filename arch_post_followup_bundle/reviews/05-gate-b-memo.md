# Architecture gate memo

## Gate
- `Gate B`

## Reviewed subbundles
- `04-runtime-row-singularity-and-db-uniqueness-hardening`
- `05-workspace-pending-persistence-quiescence-and-action-ordering`

## Decision
- `Pass`

## Gate questions and answers
1. Does the database now protect runtime singularity strongly enough to match the service code’s assumptions?
   - Answer: `Yes.` `ProcessStepRun` now has a unique `(ProcessRunId, StepDefinitionId)` index, assignments now have filtered unique run-scoped and step-scoped indexes, and the generated SQLite/PostgreSQL migrations plus integration proof confirm both providers enforce the same singularity contract.
2. Can the workspace still publish, delete, or export against stale or racing definition state?
   - Answer: `No.` The workspace now routes publish, delete, export, save, and definition switching through a shared quiescence helper that cancels pending autosave, drains outstanding debounced persistence tasks, waits for the save gate, and only then continues.
3. Do the new tests prove both the DB uniqueness side and the UI quiescence side?
   - Answer: `Yes.` `runtime-uniqueness-integration.trx` covers provider-backed uniqueness and concurrent assignment behavior, while `workspace-quiescence-components.trx` and `workspace-quiescence-integration.trx` cover pending autosave with publish, delete, and export plus broader process integration regression coverage.

## Evidence reviewed
- Commands:
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessSchemaIntegrationTests|FullyQualifiedName~ProcessesServiceIntegrationTests" --logger "trx;LogFileName=runtime-uniqueness-integration.trx" --results-directory .codex-test-results\runtime-uniqueness-integration -v:minimal`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests" --logger "trx;LogFileName=workspace-quiescence-components.trx" --results-directory .codex-test-results\workspace-quiescence-components -v:minimal`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" --logger "trx;LogFileName=workspace-quiescence-integration.trx" --results-directory .codex-test-results\workspace-quiescence-integration -v:minimal`
- Proof artifacts:
- `C:\repositories\CanDoItAll\.codex-test-results\runtime-uniqueness-integration\runtime-uniqueness-integration.trx`
- `C:\repositories\CanDoItAll\.codex-test-results\runtime-uniqueness-sqlite.sql`
- `C:\repositories\CanDoItAll\.codex-test-results\runtime-uniqueness-postgresql.sql`
- `C:\repositories\CanDoItAll\.codex-test-results\workspace-quiescence-components\workspace-quiescence-components.trx`
- `C:\repositories\CanDoItAll\.codex-test-results\workspace-quiescence-integration\workspace-quiescence-integration.trx`
- Important diffs:
- `ProcessRuntimeEntityConfigurations.cs` now defines the runtime uniqueness indexes the service layer already assumed.
- `ProcessPersistenceConstraintNames.cs` centralizes the new provider-stable index names.
- `ProcessesService.Runtime.Operations.cs` now resolves assignment rows with singular-row semantics and explicit uniqueness-conflict handling.
- `ProcessWorkspace.Canvas.Persistence.cs` now tracks and drains debounced autosave tasks before critical actions continue.
- `ProcessWorkspace.DefinitionCrud.cs` now quiesces workspace persistence before publish, delete, and export.

## Remaining gaps
- `F004` through `F007` remain open and still block final bundle closure.

## Corrective action
- Corrective subbundle key:
- `none`
- Required rerun commands:
- `none`

## Reviewer notes
- Gate B can pass because both halves of the old assumption leak are now closed: the runtime’s one-row expectations are enforced by the database, and the workspace’s critical actions no longer outrun debounced definition persistence.
