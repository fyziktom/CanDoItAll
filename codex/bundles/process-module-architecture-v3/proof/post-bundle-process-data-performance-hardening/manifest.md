# Post-Bundle Process Data Performance Hardening Manifest

## Scope

Date: 2026-06-17

Skills applied:

- `candoitall-bundle-workflow`
- `optimizing-ef-core-queries`
- `analyzing-dotnet-performance`

This addendum records a targeted hardening pass over the current Process implementation after SB20-SB29 closure. It covers EF Core query shape, process projection hydration, event sequence allocation, launch-variable canonicity, Blazor async usage, and static regex allocation.

## Changed Files

See `bundle://proof/post-bundle-process-data-performance-hardening/changed-file-hashes.txt`.

## Implementation Summary

- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessProjectionContributor.cs`: adds no-tracking user-link reads, filters preserved links to process-definition/process-run endpoints, projects runtime state and instance plan rows into narrow local records, and avoids hydrating plan payload JSON for project-structure surface nodes.
- `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeEventStore.cs`, `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs`, `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs`, `repo://src/CanDoItAll.Processes.Persistence/ProcessPersistenceMappers.cs`: removes application-side global sequence `MAX + 1`, marks `GlobalSequence` as generated on add, and keeps per-root sequence allocation cached per append batch.
- `repo://src/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs`: canonicalizes launch-variable serialization through one normalization path, narrows lookup by JSON key/value snippets, and preserves exact in-memory verification before returning assignments.
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`: replaces static compiled regex fields with generated regex methods.
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`: replaces post-`WhenAll` `Task.Result` reads with awaited task results.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessPersistenceStoreTests.cs`: adds regression tests for generated event sequence ordering and key-specific launch-variable lookup.

## Validation

Focused unit validation passed:

```text
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProcessPersistenceStoreTests|FullyQualifiedName~ProcessProjectionPipelineTests|FullyQualifiedName~ProcessRuntimeDispatchApplicationServiceTests" --no-restore --logger "console;verbosity=minimal" -p:OutDir="%TEMP%\CanDoItAllTestOut-12652\"

Result: passed, 23/23 tests.
```

Default output validation was blocked by an already-running `CanDoItAll.Web` process locking DLLs in `src/CanDoItAll.Web/bin`. The focused unit run used a temporary output directory to avoid stopping the user's running app.

Focused integration validation ran:

```text
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectStructureAgentIntegrationTests" --no-restore --logger "console;verbosity=minimal" -p:BaseOutputPath=C:\repositories\CanDoItAll\artifacts\test-bin\

Result: 25/26 passed. One failure remains in StartProcessSubprocessAsync_inherits_parent_context_and_links_child_run at tests/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs:519.
```

The failing integration assertion expects the prompt text `Do not block only because a slot-id directory is absent`. A repository search found that text only in the integration test, not in the current process templates or prompt builder. This is recorded as a prompt-template/test drift, not as a failure of the data-access hardening.

## Production Behavior Artifact Matrix

See `bundle://proof/post-bundle-process-data-performance-hardening/semantic-invariants.md`.
