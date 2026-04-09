# Phase07 persistence migrations and seed repair

## Status

- `Blocked`

## Objective

- Reopen only if later evidence shows the process MCP introduced new migration, current-profile bootstrap, or seed-data defects.

## Covered Inputs

- `REQ-024`
- `REQ-025`

## Prerequisites

- `C:\repositories\CanDoItAll\process-management-bundle\subbundles\26-process-local-mcp-server-and-tool-contracts\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\reviews\01-execution-report.md`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\CanDoItAllDatabaseMigrationBootstrap.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\ProcessMcpDatabaseBootstrapper.cs`
- `C:\repositories\CanDoItAll\CanDoItAll.Mcp.Processes.settings.json`

## Deliverables

- Explicit repair work only if persistence or bootstrap regressions appear.

## Dependency Impact

- Weak proof here would make the process MCP unreliable across database profiles.

## Validation Depth

- `Persistence and bootstrap closure`

## Implementation Steps

1. Review the phase07 source and proof.
2. Keep this lane blocked unless persistence or bootstrap regressions appear.

## Scope Exceptions

- `N/A`

## Do Not Do

- Do not fork database bootstrap behavior away from the shared composition helper.

## Acceptance Checklist

- The lane remains blocked while current-profile bootstrap and migrations continue to work through shared infrastructure.

## Proof Required

- Root bundle release-build, integration-test, and install proof.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Stay blocked unless a real persistence or seed defect appears.

## Suggested Agent Prompt

```text
Reopen this lane only if phase07 introduces new persistence, bootstrap, or seed defects.
```
