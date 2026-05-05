# 06-project-structure-command-surface

## Status

- `Completed`

## Objective

Expose focused project-structure command endpoints for common development-control operations without requiring callers to fetch or post entire structures.

## Covered Inputs

- Correction item 2: node type changes, reconnect/reparent, dependencies, markers, priority, progress, process-node execution, subtree movement, and asset nodes with attachments.

## Prerequisites

- Subbundle 05 completed.
- Existing project-structure services and UI/MCP flows reviewed for reuse boundaries.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Processes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs`

## Deliverables

- Focused HTTP endpoints backed by `ProjectStructureAgentService`, `ProjectWorkbenchService`, or a shared application helper.
- Shared executor/auth behavior with the existing project-structure API where possible.
- Strongly typed request/response DTOs for narrow commands and filters.

## Dependency Impact

- Subbundle 07 depends on this architecture decision because process-node execution connects project structure, HR matching, and process runtime behavior.

## Validation Depth

- Critical correction foundation.

## Implementation Steps

1. Review existing project-structure UI/MCP service methods for each requested command.
2. Add or extract shared helpers only where existing logic is UI-local and needed by API.
3. Add focused route mappings and DTOs for node type, reconnect, dependencies, markers, progress/priority, execution, subtree movement, and asset attachment retrieval.
4. Apply optional JWT authorization to every new route group.
5. Add representative tests and source-review proof.

## Scope Exceptions

- If full cross-project subtree copying with binary attachment duplication is unsafe in this slice, record the exact blocker and ship the service-backed move/attachment-read operations instead of inventing partial cloning semantics.

## Do Not Do

- Do not duplicate process launch or project mutation logic in endpoint lambdas.
- Do not bypass lease, result, or service error handling already used by project-structure services.
- Do not add broad all-object dumps where a focused command/query exists.

## Acceptance Checklist

- Node type, reconnect/reparent, dependency, marker, priority/progress, run/process execution, subtree movement, and asset attachment operations are represented or explicitly blocked.
- Endpoints reuse existing services/shared helpers.
- Endpoint filters allow focused retrieval.

## Proof Required

- Representative tests or source-level proof for each new command category.
- Build web project.

## Proof Captured

- Added focused `ProjectStructureAgentApi` routes for node type/status/progress/markers/priority, reparenting, dependency/link mutation, process-definition linking, process-node start, subtree transfer, and asset create/content retrieval.
- Added `ProjectStructureProcessNodeService` to reuse `ProcessesService` launch-plan, HR matching, approval, provisioning, and execution flows instead of duplicating process runtime logic in endpoint lambdas.
- Added `ProjectStructureAgentApi_supports_focused_node_dependency_and_asset_commands`.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -v:minimal` passed.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ApiIntegrationTests|FullyQualifiedName~ProjectStructureAgentApiIntegrationTests" -v:minimal` passed.

## Browser Validation Logging

- N/A. API-only subbundle.

## Progression Gate

- Downstream process/agent expansion may start only after project-structure command coverage is implemented or explicitly blocked with a reason.

## Suggested Agent Prompt

```text
Add focused project-structure API commands backed by existing services. Extract shared helpers for UI-local process-node behavior instead of duplicating it.
```
