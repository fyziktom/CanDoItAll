# 01 Shared MCP Host Bootstrap

## Status

- Status: `Completed`

## Objective

Extract repeated MCP host configuration, logging, and options registration into shared Core helpers, then migrate multiple MCP server `Program.cs` files without changing tool registration or startup behavior.

## Covered Inputs

- N001 multiple MCP servers.
- N003 preserve all functions.
- N004 proper isolation of shared helpers.
- N006 better testability.

## Prerequisites

- Prepared bundle validation has passed.
- Worktree remains clean or unrelated user changes are identified before editing.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\CanDoItAll.Mcp.Core.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\GlobalUsings.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\Program.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Program.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Program.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\Program.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\Program.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Program.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.DotNetWatch.Tests\InfrastructureTests.cs

## Deliverables

- A shared Core helper for MCP settings sources, stdio logging, backend logging where useful, and validated options registration.
- Migrated host setup in at least CodeAnalytics, Components, Processes, ProjectStructure, SshOps, and DotNetWatch.
- Targeted tests that verify shared helper behavior.

## Dependency Impact

- This is the critical foundation for the rest of the bundle.
- Subbundles 02 and 03 must not start until this helper builds and tests pass.
- If the helper changes configuration or logging semantics, all downstream proof is untrustworthy.

## Validation Depth

- Run targeted helper tests.
- Build affected MCP server projects after migration.
- Inspect diffs to ensure tool registration remains server-local and public tool methods remain intact.

## Implementation Steps

- Add a Core helper class under a server-agnostic namespace such as `CanDoItAll.Mcp.Core.Hosting`.
- Add minimal package references to Core only if required by the helper APIs.
- Replace repeated settings/logging/options code in server `Program.cs` files with the helper.
- Keep server-specific service registrations and `.WithTools<TTools>()` calls in each server project.
- Add targeted xUnit tests for configuration source loading and options registration.

## Do Not Do

- Do not move `ModelContextProtocol` server registration into Core.
- Do not change settings file names or environment variable prefix.
- Do not change public MCP tool methods, names, descriptions, request types, or response types.
- Do not alter unrelated application modules or UI code.

## Acceptance Checklist

- Shared helper compiles in `CanDoItAll.Mcp.Core`.
- Multiple MCP server hosts use the shared helper.
- Existing tool registrations remain explicit in each server `Program.cs`.
- Targeted tests pass.
- Focused build passes for affected MCP projects.

## Proof Required

- `dotnet test tests\CanDoItAll.Mcp.DotNetWatch.Tests\CanDoItAll.Mcp.DotNetWatch.Tests.csproj`
- Focused `dotnet build` or dotnetwatch build covering the migrated MCP projects.
- Execution report updated with command outcomes and closure gate decision.

## Browser Validation Logging

- N/A. This subbundle changes server-side host setup only and has no browser-rendered surface.

## Progression Gate

- Continue to subbundles 02 and 03 only after helper tests and focused build pass and no public MCP tool registration was removed.

## Suggested Agent Prompt

Implement subbundle 01. Read this README, root bundle README, plan, and traceability first. Extract shared MCP host setup into Core helpers, migrate the repeated server `Program.cs` setup, add targeted tests, run the required proof, and update `reviews/01-execution-report.md`.
