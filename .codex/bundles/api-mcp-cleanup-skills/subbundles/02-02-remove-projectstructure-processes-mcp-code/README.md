# 02 Remove ProjectStructure And Processes MCP Code

## Status

- `Completed`

## Objective

Remove the two MCP adapter projects, their dedicated tests, and active references from the solution and integration tests.

## Covered Inputs

- Original request item 2.
- R-003.

## Prerequisites

- Subbundle 01 closed or explicitly blocked with no behavior loss.

## Exact Source References

- C:\repositories\CanDoItAll\CanDoItAll.slnx
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj

## Removed Targets

- `src/CanDoItAll.Mcp.ProjectStructure`
- `src/CanDoItAll.Mcp.Processes`
- `tests/CanDoItAll.Mcp.ProjectStructure.Tests`
- `tests/CanDoItAll.Mcp.Processes.Tests`

## Deliverables

- Solution no longer includes removed MCP projects/tests.
- Deleted source/test directories for the two MCP adapters.
- Integration tests no longer compile against removed MCP assemblies.

## Dependency Impact

- Reinstall script cleanup depends on these project paths disappearing.
- Build proof depends on removing stale project references.

## Validation Depth

- Active-source search and solution build/test proof.

## Implementation Steps

1. Remove solution entries and project references.
2. Delete dedicated MCP source/test directories.
3. Delete or update integration tests that specifically launch those MCPs.

## Do Not Do

- Do not remove other MCP servers.
- Do not remove project-structure or process application modules.

## Acceptance Checklist

- No active csproj references to the removed MCP projects remain.
- No active integration test imports removed MCP namespaces.

## Proof Required

- `git grep` for removed project names across active source/scripts/config after cleanup.
- Build/test command result in execution report.

## Closure Proof

- Removed ProjectStructure and Processes MCP source projects and dedicated test projects from the solution.
- Deleted MCP-specific integration/stdio tests and stale test project references.
- `git grep` for removed MCP config names now only finds stale-section cleanup in `tools/Reinstall-CanDoItAllMcps.ps1`.
- Solution build passed in managed operation `op_9ed73ef1397a4bdc9371b8e7dfe27cfe`.

## Browser Validation Logging

- Not UI-relevant.

## Progression Gate

- Reinstall/config cleanup starts only after active references are gone.

## Suggested Agent Prompt

Remove only the two MCP adapter projects and their test references. Keep shared modules and other MCP projects intact.
