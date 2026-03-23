# Implementation Prompts

## Prompt 1: Fix Wait Semantics And Watch Lifecycle Modeling

You are improving `CanDoItAll.Mcp.DotNetWatch` so an AI agent can trust live-edit synchronization.

Read these files first:

- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Health/HealthServices.cs`
- `src/CanDoItAll.Web/Program.cs`
- `qa-mcp-dotnet-watch-improvements/01-findings.md`
- `qa-mcp-dotnet-watch-improvements/02-speed-and-evidence.md`

Implement the following:

- parse the real `dotnet watch` lifecycle messages observed on Windows
- invalidate stale healthy state as soon as watch begins processing a change
- make app waits generation-aware so they do not succeed before the active watch generation completes
- preserve and use `watchIteration` end to end

Acceptance criteria:

- `Healthy` does not succeed during rebuild output
- `QuietSinceCursor` does not finish before later watch logs for the same generation
- a C# add/delete change updates generation-tracking data and can be waited on deterministically

Do not stop at code changes. Add or update automated tests that cover the repaired behavior.

## Prompt 2: Fix Self-Host Test Isolation

You are improving the MCP server so it can validate itself while it is live.

Read these files first:

- `src/CanDoItAll.Mcp.DotNetWatch/Program.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
- `tests/CanDoItAll.Mcp.DotNetWatch.Tests/InfrastructureTests.cs`
- `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/McpServerIntegrationTests.cs`
- `qa-mcp-dotnet-watch-improvements/01-findings.md`

Implement a fix for the current file-lock failure where `candoitall_tests_run` against the MCP server's own projects fails with `MSB3021/MSB3027`.

Acceptance criteria:

- the live server can run `tests/CanDoItAll.Mcp.DotNetWatch.Tests`
- the fix is explicit and documented
- tests cover the chosen isolation strategy

Also align `CanDoItAll.slnx` with the integration-test project inventory exposed by settings and workspace info.

## Prompt 3: Improve Agent-Facing Observability

You are improving the agent UX for `CanDoItAll.Mcp.DotNetWatch`.

Read these files first:

- `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Health/HealthServices.cs`
- `src/CanDoItAll.Web/Program.cs`
- `qa-mcp-dotnet-watch-improvements/04-implementation-plan.md`

Implement better status/wait payloads so an agent can tell:

- watcher PID vs active child runtime PID
- expected watch iteration vs confirmed watch iteration
- whether the most recent change ended in hot reload, restart-needed, rebuild, or failure

Acceptance criteria:

- `app_status` becomes sufficient for a browser-driving agent to decide whether refresh is safe
- the new fields are documented and covered by tests

## Prompt 4: Full Verification Pass

You are the verification agent for the repaired MCP server.

Use:

- `qa-mcp-dotnet-watch-improvements/03-reproduction-playbook.md`
- `qa-mcp-dotnet-watch-improvements/05-regression-checklists.md`

Your job:

- rerun the reproductions
- confirm the old failures no longer happen
- record any remaining gaps

Output:

- a concise pass/fail report
- any remaining blocking issues
- suggested follow-up tests if coverage is still weak
