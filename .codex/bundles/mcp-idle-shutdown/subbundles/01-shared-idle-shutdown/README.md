# shared-idle-shutdown

## Status

- `Completed`

## Objective

- Add shared, configurable idle shutdown behavior and wire it into the Components and SSH Ops MCP stdio hosts.

## Success Criteria

- Idle shutdown is implemented once in `CanDoItAll.Mcp.Core`.
- Components MCP and SSH Ops MCP opt into that service through strongly typed options.
- Central tool wrappers mark activity and protect active operations from idle shutdown.
- Targeted tests and builds pass.

## Covered Inputs

- N001, N002, N003
- R001, R002, R003, R004, R005

## Prerequisites

- Bundle prepared validator passes.
- Exact source references below still exist.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\Hosting\McpHostBuilderExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\CanDoItAll.Mcp.Core.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Configuration\McpServerOptions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Tools\ComponentsTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Configuration\McpServerOptions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Tools\SshOpsTools.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Components.Tests\ComponentsToolsTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`

## Deliverables

- Shared idle shutdown options, activity tracker, hosted service, and registration extension.
- Components options and settings expose a short default inactivity timeout.
- SshOps options and settings expose a longer default inactivity timeout.
- Components and SshOps tool wrappers record activity with active-operation scopes.
- Tests covering inactivity shutdown, active-operation protection, and tool wrapper activity.

## Dependency Impact

- This is the only implementation subbundle and is a critical foundation. If the shared idle logic is wrong, both requested MCPs will keep accumulating processes or may stop during active work.

## Validation Depth

- Critical foundation and process-lifecycle closure.

## Implementation Steps

1. Add shared idle shutdown options and activity tracker in `CanDoItAll.Mcp.Core.Hosting`.
2. Add a hosted service that checks inactivity and calls `StopApplication()` only when no operation is active.
3. Add a host registration helper that binds each MCP's typed options to shared idle options.
4. Add `Server.IdleShutdown` options to Components and SSH Ops settings models with per-MCP defaults.
5. Register the idle shutdown service in both Program entrypoints.
6. Inject the activity tracker into both tool classes and wrap their existing `ExecuteAsync` helpers.
7. Add focused tests for shared idle behavior and Components tool activity.
8. Run targeted tests and builds.

## Scope Exceptions

- No browser or UI validation. The behavior is stdio host lifecycle.
- No process-manager cleanup of already running stale instances. This subbundle prevents future idle accumulation.

## Do Not Do

- Do not refactor the MCP transport layer.
- Do not add a separate watchdog process.
- Do not change remote SSH operation semantics except lifecycle idle tracking.
- Do not hide configuration errors with silent defaults after validation fails.

## Acceptance Checklist

- Shared service requests shutdown after the inactivity timeout.
- Shared service does not request shutdown while an operation scope is active.
- Components default timeout is shorter than SSH Ops default timeout.
- Both requested MCPs register the idle service.
- Both requested MCPs mark tool invocations through their centralized wrappers.

## Proof Required

- `dotnet test tests/CanDoItAll.Mcp.Components.Tests/CanDoItAll.Mcp.Components.Tests.csproj --no-restore -m:1`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore -m:1`
- `dotnet build src/CanDoItAll.Mcp.Components/CanDoItAll.Mcp.Components.csproj --no-restore -m:1`
- `dotnet build src/CanDoItAll.Mcp.SshOps/CanDoItAll.Mcp.SshOps.csproj --no-restore -m:1`
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --stage completed .codex\bundles\mcp-idle-shutdown`

## Browser Validation Logging

- N/A. This subbundle changes stdio host lifecycle behavior only.

## Progression Gate

- Passed. Targeted tests and MCP builds passed, execution report rows are updated, and raw notes N001 through N003 are solved.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
