# Remote Project-Structure MCP Client, Filters, And Cross-Machine Setup

## Status

- `Completed`

## Objective

- Build the new local stdio MCP client that connects to the central CanDoItAll machine, shapes filtered responses for agent context, and ships with deterministic install and config support for other workstations.

## Covered Inputs

- `R001`, `R002`, `R004`, `R009`, `R014`, `R017`
- `N001`, `N002`, `N003`, `N007`, `N008`, `N012`

## Prerequisites

- `01-central-project-structure-agent-api-locking-checklist-import-and-analytics-foundation` completed with trusted contracts
- `02-agent-policy-settings-and-knowledge-guidance-in-candoitall-web` completed with trusted settings schema and setup guidance

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\Configuration.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\ProjectStructureHttpClient.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\ProjectStructureCoordinator.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.ProjectStructure.Tests\ProjectStructureCoordinatorTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.ProjectStructure.Tests\ProjectStructureToolsTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectStructureMcpIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tools\Install-CanDoItAllProjectStructureMcp.ps1`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\docs\project-structure-mcp-setup.md`
- `C:\repositories\CanDoItAll\.vscode\mcp.json`

## Deliverables

- New `CanDoItAll.Mcp.ProjectStructure` project with typed tool definitions.
- HTTP client and settings validation for connecting to the main CanDoItAll machine.
- MCP-side filtering and field-shaping support to reduce agent context.
- Updated reinstall or config tooling for the new MCP server.
- Tests that cover tool shaping, error mapping, and real integration against the central API.

## Dependency Impact

- `04` depends on this subbundle because the request is specifically about a new MCP, not only a web API.
- Weak proof here invalidates the remote-workstation deployment story and the final validation chain.

## Validation Depth

- `Critical execution bridge`
- `Tool tests, integration tests, and setup verification`

## Implementation Steps

1. Add the new MCP project, settings model, tool contracts, and transport startup.
2. Implement HTTP client calls to the central API with explicit error mapping and no hidden fallback behavior.
3. Implement response shaping for field filtering and layout-data suppression.
4. Update reinstall and local config tooling to publish and register the new server.
5. Add tests for direct tool invocation and integration tests against the central API.

## Scope Exceptions

- If a setup shortcut or generated config flow cannot be fully automated on every machine type in this phase, document the exact manual step and keep the generated config itself deterministic.

## Do Not Do

- Do not let the MCP client talk to the local repo DB or files for project-structure operations.
- Do not hide central policy failures behind client retries or fabricated defaults.
- Do not expose raw unfiltered graph payloads as the only read option.

## Acceptance Checklist

- The new MCP starts from stdio with validated settings.
- Read tools can return compact filtered node data without layout noise by default.
- Mutation tools return clear policy and lock failures.
- Setup tooling publishes and configures the new MCP alongside existing CanDoItAll MCPs.
- Integration tests prove the MCP can talk to the central API.

## Proof Required

- `dotnet test` for the new MCP tool project
- `dotnet test` for MCP integration against a real web host
- Generated settings example and updated reinstall output
- Logged example of filtered node output and a lock-conflict or approval-conflict output

## Browser Validation Logging

- `- N/A for this subbundle because the shipped behavior is a local stdio MCP client and setup tooling, not a browser surface.`
- `- The browser proof for operator-facing setup guidance belongs to subbundle 02 and final closure smoke in 04.`

## Progression Gate

- The MCP client must complete real integration proof against the central API and the setup tooling must be updated before the final closure audit may start.

## Suggested Agent Prompt

```text
Implement the new CanDoItAll.Mcp.ProjectStructure server as a thin remote client against the central CanDoItAll web API. Keep tool contracts typed, shape responses for context reduction, and update the workstation reinstall/config flow in the same phase.
```
