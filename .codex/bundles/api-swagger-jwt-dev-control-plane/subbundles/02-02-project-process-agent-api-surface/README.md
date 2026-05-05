# 02-project-process-agent-api-surface

## Status

- `Completed`

## Objective

Expose project, process, launch-plan, and agent API groups that reuse existing services and include process filtering for focused development access.

## Covered Inputs

- N001 API access to projects, processes, and agents.
- N002 Unify existing logic and avoid duplicated behavior.
- N003 Development API access when MCP ports differ.
- N004 Map helpful project/process development controls.
- N005 Process run detail, manager chat/direct message, and edit processes.
- N006 Project-structure node process flow and HR resource matching.
- N007 Process filtering.

## Prerequisites

- `01-01-api-foundation-auth-swagger` completed and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.Operations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Messaging\ProcessesService.DirectMessaging.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Staffing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Workspace\CurrentProfileAgentFrameworkWorkspaceService.cs`

## Deliverables

- `/api/dev/projects` group.
- `/api/dev/processes` group with definitions, runs, filtered detail, runtime operations, templates, launch plans, HR matching, and direct messages.
- `/api/dev/agents` group with catalog/editor/chat/execution history operations.
- Shared HTTP result mapping for `Result` failures and deterministic errors.
- Existing `/api/project-structure-mcp` remains active and covered by auth helper.

## Dependency Impact

- This is a critical functional foundation. Settings and final proof depend on the surface being service-backed and not duplicative.

## Validation Depth

- Critical foundation with service-reuse source review.

## Implementation Steps

1. Add typed API endpoint extension files.
2. Add process run detail filter DTO/result.
3. Map project endpoints using `ProjectsService`.
4. Map process endpoints using `ProcessesService`.
5. Map agent endpoints using `IAgentFrameworkWorkspaceService`.
6. Apply conditional authorization to every API group.
7. Add representative tests.

## Scope Exceptions

- Existing project-structure API is not duplicated under `/api/dev/projects`; it remains at `/api/project-structure-mcp` to preserve current clients.

## Do Not Do

- Do not copy `ProcessesCoordinator` into the web project.
- Do not write directly to EF entities where a public service method exists.
- Do not add broad unfiltered all-data endpoints when a specific query exists.

## Acceptance Checklist

- Project endpoints call `ProjectsService`.
- Process endpoints call `ProcessesService`.
- Agent endpoints call `IAgentFrameworkWorkspaceService`.
- Filtered run detail returns only requested subsets.
- HR matching and launch execution are reachable through API routes.

## Proof Required

- Targeted integration tests for project/process/agent route smoke.
- Targeted process filter test.
- Source review recorded in execution report.
- Build web project.

## Browser Validation Logging

- N/A. API-only subbundle.

## Progression Gate

- Downstream work may continue only after endpoint tests pass and architecture review confirms no duplicated service logic.

## Suggested Agent Prompt

```text
Implement only the project/process/agent API surface. Keep handlers thin and reuse existing services. Add filtering for process run detail.
```
