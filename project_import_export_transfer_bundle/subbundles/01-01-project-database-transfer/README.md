# 01-project-database-transfer

## Status

- `Completed`

## Objective

Implement the critical database-to-database all-projects transfer foundation through the existing `IDatabaseTransferHandler` system.

## Covered Inputs

- `N001`: add system for export/import all projects
- `N003`: transfer between existing databases via UI
- `N004`: use the same transfer model as processes/agents/etc.

## Prerequisites

- Prepared bundle readiness gate has passed.
- No prerequisite implementation subbundle.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseTransferModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseTransferService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\DatabaseTransfer\ProcessDefinitionsDatabaseTransferHandler.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Services\WorkbenchModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodes\ProjectNodeBindings.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodes\ProjectNodeLifecycleHistory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAssemblyService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\DatabaseRuntimeSwitchingIntegrationTests.cs`

## Deliverables

- A registered `Projects` database-transfer handler.
- Preview counts and warnings for source/target project data.
- Transfer logic that copies core project records and workbench records in safe order.
- Integration coverage for source profile to target profile project transfer.

## Dependency Impact

- Subbundles `02` and `03` depend on this foundation.
- If this phase omits records or has weak proof, zip import/export and UI transfer proof are invalid.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add shared project transfer helper logic in `CanDoItAll.Modules.Workbench`.
2. Add `ProjectsDatabaseTransferHandler` with descriptor key `projects`, label `Projects`, and sort order near content groups.
3. Ensure project and workbench schema initializers run before counts/copy.
4. Implement preview counts for projects and workbench records.
5. Implement target clearing in reverse dependency order and copying in dependency order.
6. Register the handler in `WorkbenchModuleServiceCollectionExtensions`.
7. Add integration tests that seed projects with hierarchy and structure data, transfer to another profile, and verify both services can read the result.

## Scope Exceptions

- Do not copy ProjectStructure leases or operation analytics in this phase.
- Do not copy unrelated project-linked module artifacts unless they are in the project/workbench table inventory.

## Do Not Do

- Do not modify generic infrastructure transfer contracts unless project transfer cannot be implemented as a handler.
- Do not alter process, agent, provider, or MCP-token handlers.
- Do not implement zip packaging in this subbundle.

## Acceptance Checklist

- `Projects` transfer preview is available when source has at least one project.
- Transfer succeeds into an empty target profile.
- Transferred projects preserve phases, options, hierarchy links, workbench nodes, object links, node bindings, node references, lifecycle events, projection layouts, view states, and cross-module mutation records where present.
- Existing transfer handlers still register.

## Proof Required

- `dotnet build src/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj --no-restore`
- `dotnet build tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --no-dependencies -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~DatabaseTransferIntegrationTests.Project_transfer_copies_all_project_and_workbench_records_between_profiles" --logger "console;verbosity=normal"`

## Browser Validation Logging

- `N/A` for this subbundle. Browser proof is owned by `03-ui-exposure-and-workflow-proof`.

## Progression Gate

- Passed. Integration proof shows database-to-database project transfer preserves the scoped project/workbench aggregate.

## Suggested Agent Prompt

```text
Implement subbundle 01 only: add the Projects database-transfer handler, register it, and prove profile-to-profile all-project transfer with targeted tests. Do not implement zip or UI controls yet.
```
