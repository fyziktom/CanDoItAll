# runtime-state-overview-service

## Status

- `Completed`

## Objective

Create the generic process runtime state projection service and move process page run-state badges to that projection so active, blocked, and failed counts are accurate.

## Covered Inputs

- N001, N002, N003, N004
- R001, R002, R003, R004

## Prerequisites

- Bundle readiness gate passed.
- No prerequisite implementation subbundle.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Definitions\ProcessDefinitionEditorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeViewModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.DefinitionListQuery.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Deliverables

- Scoped process runtime state overview service registered in DI.
- Strongly typed run status count models.
- Page header and definition list badges use separated active, blocked, and failed counts.
- Existing misleading active count corrected so blocked runs are not counted as active.
- Service exposes explicit invalidation/reload semantics for later UI mutations.

## Dependency Impact

- Subbundles 02 and 03 depend on this service. If this projection has wrong count semantics or becomes a competing state owner, lazy loading and stop behavior will be built on an unreliable foundation.

## Validation Depth

- Critical foundation with integration-test proof and browser-visible badge proof.

## Implementation Steps

1. Add strongly typed count/projection records for runtime state overview.
2. Add a scoped projection service that reads existing process runtime sources and caches only scoped, derivable snapshots.
3. Register the service in `ProcessesModuleServiceCollectionExtensions`.
4. Replace page header/list badge count sources with the new projection.
5. Correct existing active-count query semantics if the old `ActiveRunCount` remains in use.
6. Add integration test coverage for active, blocked, and failed count separation.

## Scope Exceptions

- Durable distributed caching for Manager-agent use is not implemented in this subbundle. The service shape must support future consumption, but persistence remains authoritative.

## Do Not Do

- Do not mutate process state in the projection service.
- Do not introduce stringly typed status classification.
- Do not remove run history visibility for blocked or failed runs.

## Acceptance Checklist

- Active count includes only `ProcessRunStatus.Active`.
- Blocked and failed counts are separately available.
- UI badge labels no longer call blocked/failed runs active.
- Service can be invalidated after mutations.
- Tests fail before/fix after for count semantics.

## Proof Required

- Targeted integration test for count separation.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter ProcessesServiceIntegrationTests`
- Browser check on `https://localhost:7271/processes` or documented blocker.

## Browser Validation Logging

- Route: `https://localhost:7271/processes` or project-scoped processes route.
- Viewport: large desktop first; narrower follow-up only if badge wrapping is changed.
- Actions/assertions: navigate, inspect header/list badges, confirm active/blocked/failed texts are readable and separated.
- Screenshots: record under `output/playwright/process-runtime-state-overview/` when browser is available.

## Progression Gate

- Downstream subbundles may continue only after count semantics are proven and the service boundary remains a projection over existing source-of-truth services.

## Suggested Agent Prompt

```text
Implement subbundle 01 only: add the process runtime state overview service, wire accurate active/blocked/failed badges to it, and prove count separation without moving canonical state out of existing runtime persistence/services.
```
