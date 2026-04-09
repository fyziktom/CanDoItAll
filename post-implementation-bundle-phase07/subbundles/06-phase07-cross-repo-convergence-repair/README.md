# Phase07 cross-repo convergence repair

## Status

- `Blocked`

## Objective

- Reopen only if later evidence shows the process MCP install-discoverability workflow drifted from the repo-standard MCP conventions or became another hidden workstation-only dependency.

## Covered Inputs

- `N12`
- `REQ-025`
- `REQ-026`

## Prerequisites

- `C:\repositories\CanDoItAll\process-management-bundle\subbundles\27-process-mcp-install-reinstall-config-and-skills\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\reviews\01-execution-report.md`

## Exact Source References

- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\tools\Install-CanDoItAllProcessesMcp.ps1`
- `C:\repositories\CanDoItAll\docs\processes-mcp-setup.md`
- `C:\repositories\CanDoItAll\codex\skills\candoitall-processes-mcp\SKILL.md`
- `C:\repositories\CanDoItAll\.vscode\mcp.json`
- `C:\repositories\CanDoItAll\.artifacts\mcp-installs\install-manifest.json`

## Deliverables

- Explicit repair work only if install, config, manifest, or skill-sync drift appears.

## Dependency Impact

- Weak proof here would make the process MCP difficult to discover or reproduce on another workstation.

## Validation Depth

- `Install-discoverability closure`

## Implementation Steps

1. Review the phase07 install and config proof.
2. Keep this lane blocked unless install-discoverability drift appears.

## Scope Exceptions

- `N/A`

## Do Not Do

- Do not let the process MCP become another manually wired workstation-only tool.

## Acceptance Checklist

- The lane remains blocked while reinstall, focused install, config updates, manifest updates, and skill sync stay aligned with repo conventions.

## Proof Required

- Root bundle reinstall, focused install, config inspection, manifest inspection, and skill-sync inspection.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Stay blocked unless a real install-discoverability defect appears.

## Suggested Agent Prompt

```text
Reopen this lane only if the process MCP stops following the repo-standard install and discoverability workflow.
```
