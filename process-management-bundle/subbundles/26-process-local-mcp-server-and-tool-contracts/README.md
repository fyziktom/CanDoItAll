# 26 Process Local MCP Server And Tool Contracts

## Status

- `Completed`

## Objective

- Add a simple local `CanDoItAll.Mcp.Processes` stdio server that exposes process definitions and runtime data through MCP tools while reusing canonical process services and bootstrap paths.

## Covered Inputs

- `REQ-023`
- `REQ-024`
- `REQ-026`
- User request for a simple MCP server for processes and their definitions

## Prerequisites

- `24-post-implementation-bundle-phase06-generation`
- `05-process-definition-lifecycle-and-governance-model`
- `09-runtime-state-machine-approvals-and-decision-rights`
- `11-journal-forensics-operating-modes-and-import-export`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\architecture\01-target-solution.md`
- `C:\repositories\CanDoItAll\process-management-bundle\analysis\01-current-state.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Reads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\SwitchableAppDbContextFactory.cs`

## Deliverables

- A new `CanDoItAll.Mcp.Processes` source project in the solution.
- Strongly typed MCP tool contracts for process-definition and process-runtime access.
- Local bootstrap/configuration that uses canonical process services and active database-profile infrastructure.
- Focused unit and integration coverage plus real stdio transport proof.

## Dependency Impact

- This is the external automation surface for the process module.
- If it duplicates business rules or storage access instead of reusing the process module, the repo will gain a second process contract that drifts immediately.

## Validation Depth

- `Critical service-boundary and transport proof`

## Implementation Steps

1. Create a new local stdio MCP project modeled after the existing repo MCP servers, not after the remote project-structure agent API.
2. Bootstrap only the infrastructure and module registrations required to resolve canonical process services against the active database profile.
3. Expose a compact, typed tool surface for listing and reading definitions, loading definition editor data, listing runs and runtime details, and invoking the smallest justified mutation set.
4. Keep tool error behavior explicit and structured instead of silently swallowing validation or runtime failures.
5. Add focused unit tests, integration tests, and a real stdio transport test that proves the server starts and returns structured content.

## Scope Exceptions

- Do not build a new web-based process-agent API in this phase.
- Do not expand into full process authoring over MCP if the current request only needs simple access to definitions and runtime state.

## Do Not Do

- Do not bypass `ProcessesService` with hand-written SQL or duplicate business orchestration.
- Do not introduce stringly typed pseudo-contracts when typed request and response models already exist or can be defined cleanly.
- Do not make the MCP depend on the full web host startup path when a smaller composition root is enough.

## Acceptance Checklist

- `CanDoItAll.Mcp.Processes` builds and is included in the solution.
- The MCP exposes typed tools for process-definition and runtime access.
- The MCP reuses canonical process services and active database-profile bootstrap logic.
- Unit tests and integration tests cover the main success and structured-failure paths.
- Real stdio proof confirms tool startup and invocation.

## Proof Required

- `dotnet build` for the new MCP project.
- Focused unit tests for tool-envelope and error mapping behavior.
- Focused integration and stdio transport tests proving real tool calls.
- Code review confirmation that the MCP reuses canonical process services instead of duplicating domain logic.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Phase07 may not move to install/config closure until the MCP server itself builds, tests, and proves real stdio transport success.

## Suggested Agent Prompt

```text
Implement only the local process MCP server slice. Create a simple stdio MCP over canonical process services, keep it strongly typed, avoid a second process API or duplicated business logic, and close only after focused unit, integration, and stdio transport proof pass.
```
