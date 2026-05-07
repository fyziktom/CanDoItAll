# 07-process-agent-command-surface

## Status

- `Completed`

## Objective

Add focused process and agent API commands/filters that reduce payload size and expose the control operations needed during iterative development.

## Covered Inputs

- Correction items 3 and 4: process and agent APIs need the same depth improvements as projects.

## Prerequisites

- Subbundles 05 and 06 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.Runtime.Operations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Workspace\CurrentProfileAgentFrameworkWorkspaceService.cs`

## Deliverables

- Process endpoints for step-scoped artifacts, assignments, manager/direct-message actions, launch-plan matching/execution shortcuts, and narrow runtime controls where existing services support them.
- Agent endpoints for focused catalog, chat/session, execution-run, approval, and artifact/log access where existing services support them.
- No direct persistence duplication where a public service method already exists.

## Dependency Impact

- Reclosure depends on these focused endpoints because the corrected scope is not satisfied by broad list/detail endpoints alone.

## Validation Depth

- Focused functional expansion with representative tests.

## Implementation Steps

1. Review current process and agent API route coverage against existing service methods.
2. Add focused route aliases or commands that reduce request/response payloads.
3. Keep logic in service calls and avoid direct persistence access.
4. Add representative tests/source-review proof.

## Do Not Do

- Do not create a parallel process runtime or agent execution model in the web project.
- Do not silently ignore unsupported operations; record explicit blockers.

## Acceptance Checklist

- Process routes cover focused step/artifact/message/runtime operations.
- Agent routes cover focused catalog/session/execution/approval operations.
- Route names and DTOs use concise API naming.

## Proof Required

- Targeted integration tests or route/service source review for representative process and agent commands.
- Build web project.

## Proof Captured

- Added process run/step focused endpoints for single step reads, step-scoped artifacts/assignments, single artifact reads, run manager directives, direct messages, step transitions, agent-step reruns, assignment resolution, and step artifact recording.
- Added agent-scoped execution endpoints for start/list/detail plus focused artifacts, log, metrics, approvals, checkpoints, and tool receipts.
- All new endpoints delegate to `ProcessesService` or `IAgentFrameworkWorkspaceService`.
- Added `Api_filters_process_run_artifacts_by_artifact_id` coverage for step-scoped artifact and single-artifact routes.
- Added `Api_openapi_exposes_focused_control_plane_routes` coverage for project-structure, process, and agent focused routes.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -v:minimal` passed.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ApiIntegrationTests|FullyQualifiedName~ProjectStructureAgentApiIntegrationTests" -v:minimal` passed.

## Browser Validation Logging

- N/A. API-only subbundle.

## Progression Gate

- Reclosure may start only after representative process/agent command proof exists.

## Suggested Agent Prompt

```text
Expand process and agent API surfaces with focused commands and filters. Reuse existing services and keep endpoint logic thin.
```
