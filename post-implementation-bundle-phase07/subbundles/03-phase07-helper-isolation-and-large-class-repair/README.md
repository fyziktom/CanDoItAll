# Phase07 helper isolation and large-class repair

## Status

- `Blocked`

## Objective

- Reopen only if later evidence shows the phase07 MCP or installer work introduced oversized files, hidden helper debt, or untestable orchestration.

## Covered Inputs

- `REQ-024`
- `REQ-025`

## Prerequisites

- `C:\repositories\CanDoItAll\process-management-bundle\subbundles\26-process-local-mcp-server-and-tool-contracts\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\subbundles\27-process-mcp-install-reinstall-config-and-skills\README.md`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\ProcessesTools.cs`
- `C:\repositories\CanDoItAll\tools\Install-CanDoItAllProcessesMcp.ps1`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`

## Deliverables

- Explicit repair work only if helper isolation or file-size regressions appear.

## Dependency Impact

- Weak proof here would let maintenance debt creep into the new automation surface.

## Validation Depth

- `Maintainability closure`

## Implementation Steps

1. Review the phase07 source and proof.
2. Keep this lane blocked unless helper or file-size regressions appear.

## Scope Exceptions

- `N/A`

## Do Not Do

- Do not accept large-script or large-class growth just because phase07 is tooling-oriented.

## Acceptance Checklist

- The lane remains blocked while the added files stay focused and testable.

## Proof Required

- Root bundle build, test, and source review evidence.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Stay blocked unless a real maintainability defect appears.

## Suggested Agent Prompt

```text
Reopen this lane only if phase07 tooling adds oversized or poorly isolated orchestration.
```
