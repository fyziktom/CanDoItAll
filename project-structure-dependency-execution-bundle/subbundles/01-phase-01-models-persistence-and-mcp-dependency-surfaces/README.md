# Phase 01 models persistence and MCP dependency surfaces

## Status

- `Completed`

## Objective

- Add explicit duration support, dependency deletion and read models, and MCP-ready dependency metadata so the rest of the feature can build on stable graph semantics.

## Covered Inputs

- `N001`
- `N002`
- `N008`
- `N010`
- `RQ-001`
- `RQ-002`
- `RQ-009`
- `RQ-011`
- `NFR-001`
- `NFR-002`

## Prerequisites

- none

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.SharedKernel\ProjectObjectContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureChecklistService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAgentContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAgentService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\project-structure-dependency-execution-bundle\requirements\01-normalized-requirements.md

## Deliverables

- Explicit node-duration field in seconds carried through persistence and relevant summaries or contracts.
- Service-layer support for removing dependency links and querying dependency-centric node information.
- MCP or project-structure read surface updated to expose dependency and duration data needed by later consumers.
- Integration tests covering many-to-many dependency persistence, duration defaults or storage, and link deletion behavior.

## Dependency Impact

- Phase 02 depends on the graph and deletion semantics established here.
- Phase 03 depends on the duration field and dependency read models established here.
- Phase 04 cannot produce trustworthy browser proof if the persistence layer is incomplete or ambiguous.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extend shared and workbench node contracts with explicit duration-seconds support.
2. Add or expose dependency deletion APIs in the service layer and any required persistence or migration updates.
3. Update checklist and MCP-facing contracts so dependency readiness consumers can retrieve consistent graph data.
4. Add targeted integration coverage for dependency creation, deletion, and duration propagation.

## Scope Exceptions

- Toolbar UX, canvas preview, and delete-mode visuals belong to Phase 02.
- Mermaid export and browser proof belong to later phases.

## Do Not Do

- Do not claim the UI workflow is complete here.
- Do not invent a second dependency-link concept separate from `ProjectObjectLinkKind.DependsOn`.
- Do not defer duration-seconds propagation from persistence if later phases would need to guess field semantics.

## Acceptance Checklist

- Node contracts can represent explicit duration seconds.
- Dependency links can be removed without deleting nodes.
- Dependency information is exposed in a reusable shape for MCP or later graph services.
- Integration tests prove many-to-many dependency persistence and deletion behavior.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter ProjectWorkbenchServiceIntegrationTests`
- Targeted build if migrations or shared contracts change.
- Traceability check confirming duration and dependency-readiness requirements stay mapped.

## Browser Validation Logging

- N/A for direct browser closure in this phase.
- Reopen this phase if later browser proof shows that duration or dependency metadata is missing from the surface consumed by the page.

## Progression Gate

- Do not start Phases 02 or 03 until the service-layer graph semantics and duration field compile cleanly and are covered by targeted integration proof.

## Suggested Agent Prompt

```text
Implement Phase 01 only.

Add explicit duration-seconds support plus dependency deletion and MCP-facing graph metadata.
Keep DependsOn direction aligned with existing checklist semantics.
Do not move on to toolbar UX or Mermaid export in this phase.
```
