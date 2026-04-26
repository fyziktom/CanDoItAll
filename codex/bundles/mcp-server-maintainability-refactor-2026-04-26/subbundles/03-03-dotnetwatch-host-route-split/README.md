# 03 DotNetWatch Host Route Split

## Status

- Status: `Completed`

## Objective

Split DotNetWatch backend route mapping and replay execution wrapper out of the primary host `Program.cs` while preserving launch modes, backend routes, request replay, cancellation, and auth flow.

## Covered Inputs

- N002 detailed refactoring.
- N003 preserve all functions.
- N005 split too long files.
- N006 better testability.

## Prerequisites

- Subbundle 01 is completed and trusted.
- DotNetWatch tests are identified and runnable.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Program.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Backend\BackendRequestReplayStore.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Backend\BackendToolContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Backend\LocalToolInvoker.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Tools\CanDoItAllTools.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.DotNetWatch.Tests\InfrastructureTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.DotNetWatch.Tests\BundleImprovementTests.cs

## Deliverables

- A smaller DotNetWatch `Program.cs` with backend route mapping in a focused partial file or helper.
- Preserved backend route names and replay behavior.
- Targeted DotNetWatch tests/build proof.

## Dependency Impact

- Final closure depends on this subbundle because DotNetWatch is the most complex MCP host and validates that the shared helper works in a dual-host server.
- This subbundle does not unlock subbundle 02.

## Validation Depth

- Run DotNetWatch unit tests.
- Build `CanDoItAll.Mcp.DotNetWatch`.
- Inspect backend route mapping to verify every pre-existing route is still mapped to the same invoker method and route key.

## Implementation Steps

- Make `Program` partial if needed.
- Move `MapToolRoutes` and `ExecuteToolRouteAsync` into a focused file such as `Program.ToolRoutes.cs`.
- Keep route paths, route keys, request types, invoker calls, cancellation tokens, and replay store usage unchanged.
- Run DotNetWatch tests and focused build.

## Do Not Do

- Do not change backend auth middleware.
- Do not change route paths or tool route keys.
- Do not change `LaunchContext` parsing except for shared helper usage already covered by subbundle 01.
- Do not move runtime coordination logic in this subbundle.

## Acceptance Checklist

- DotNetWatch `Program.cs` is shorter and host flow is easier to scan.
- Every backend tool route is still present.
- Request replay wrapper behavior is preserved.
- DotNetWatch tests/build pass.

## Proof Required

- `dotnet test tests\CanDoItAll.Mcp.DotNetWatch.Tests\CanDoItAll.Mcp.DotNetWatch.Tests.csproj`
- Focused build for `src\CanDoItAll.Mcp.DotNetWatch\CanDoItAll.Mcp.DotNetWatch.csproj`
- Execution report updated with command outcomes and closure gate decision.

## Browser Validation Logging

- N/A. This subbundle changes server-side backend route source organization only.

## Progression Gate

- Continue to final closure only after DotNetWatch tests/build pass and route mapping equivalence is reviewed.

## Suggested Agent Prompt

Implement subbundle 03 after subbundle 01 is closed. Split backend route mapping from DotNetWatch host startup, preserve route behavior exactly, run DotNetWatch tests/build, and update the execution report.
