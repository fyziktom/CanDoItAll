# 02 Dotnet Run Tooling

## Status

- Status: `Completed`

## Objective

Implement a generic `workspace_dotnet_run` capability so agents can start and smoke-check runnable .NET projects without writing ad hoc launch scripts.

## Covered Inputs

- User request for generic .NET build/run/test readiness.
- Inventory finding that `workspace_dotnet_run` exists in tool policy but not in the command execution or seeded tool surface.

## Prerequisites

- Subbundle 01 inventory is complete.
- The intended tool behavior is generic and not tied to Blazor or any sample app topic.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Process\WorkspaceProcessContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandExecutionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandPlanBuilder.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workspace\MafAgentRuntime.WorkspaceRuntimePlugin.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkspaceCommandExecutionServiceTests.cs

## Deliverables

- `workspace_dotnet_run` command execution method.
- MAF runtime exposure and built-in tool mapping.
- Focused unit tests for command planning and tool exposure.

## Dependency Impact

- Subbundle 03 assigns the new capability to agents.
- Subbundle 04 uses the new capability or verifies agents can use it during live process validation.

## Validation Depth

- Unit tests for service contract and command arguments.
- Source scan proving no Blazor/calculator topic is embedded in the run tool.
- Build after implementation.

## Implementation Steps

- Add `DotnetRun` to `IWorkspaceCommandExecutionService`.
- Add a generic plan builder method that supports foreground console runs and HTTP startup smoke for web projects.
- Add runtime plugin and tool capability switch entries for `workspace_dotnet_run`.
- Add focused tests for generated arguments and exposed tool name.

## Scope Exceptions

- The run tool may provide generic HTTP startup proof; route-specific UI behavior remains Playwright/QA responsibility.

## Do Not Do

- Do not hardcode Blazor, calculator, converter, or validation-app topics into tool logic.
- Do not hide startup failures behind silent fallbacks.
- Do not leave long-running `dotnet run` calls blocking the agent indefinitely.

## Acceptance Checklist

- `workspace_dotnet_run` is callable as a built-in tool.
- Failed startup returns a failed receipt with actionable stdout/stderr.
- Successful HTTP startup returns URL, process id, and log paths.

## Proof Required

- Focused unit test output.
- `dotnet build` output for affected projects.
- Source scan for active sample-topic strings in the new tool files.

## Browser Validation Logging

- N/A for this subbundle. Browser proof occurs in subbundle 04.

## Progression Gate

- Subbundle 03 may start only when the run tool exists in command execution, MAF tool mapping, and tests.

## Suggested Agent Prompt

Implement generic `workspace_dotnet_run` across the workspace command service and MAF tool bridge. Keep the implementation app-type-neutral, explicitly failing on startup errors and returning durable launch evidence for web projects.

