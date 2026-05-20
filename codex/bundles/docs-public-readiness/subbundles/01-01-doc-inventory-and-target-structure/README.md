# 01-doc-inventory-and-target-structure

## Status

- `Completed`

## Objective

- Establish the source-grounded documentation inventory and target documentation structure before editing public-facing docs.

## Success Criteria

- Current docs, scripts, runtime config, and missing project READMEs are inventoried.
- Root/docs index changes have a clear target and no stale setup paths are introduced.

## Covered Inputs

- `N001`: Docs are out of date and missing new/refactored modules.
- `N005`: Remove old things for public-version readiness.

## Prerequisites

- none

## Exact Source References

- C:\repositories\CanDoItAll\README.md
- C:\repositories\CanDoItAll\docs\README.md
- C:\repositories\CanDoItAll\docs\development-runtime.md
- C:\repositories\CanDoItAll\CanDoItAll.slnx
- C:\repositories\CanDoItAll\tools\Install-CanDoItAllWebApp.ps1
- C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1
- C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1

## Deliverables

- Updated documentation inventory in the bundle.
- Updated root/docs index targets if implementation finds a navigation gap.

## Dependency Impact

- Subbundles 02 and 03 depend on this inventory. If it misses a script or project, setup docs and README coverage become false.

## Validation Depth

- Critical documentation inventory foundation.

## Implementation Steps

1. Re-run project README coverage inventory if needed.
2. Compare root/docs index claims against runtime config and setup scripts.
3. Update bundle execution report with inventory proof.

## Scope Exceptions

- This phase does not add every missing project README; subbundle 03 owns that.

## Do Not Do

- Do not change runtime code or project files.
- Do not mark retired Processes/ProjectStructure MCP servers as active.

## Acceptance Checklist

- Inventory lists all missing project READMEs found at preparation time.
- Setup scripts and runtime config are identified by real paths.

## Proof Required

- Project README coverage inventory command output recorded in the execution report.
- Manual source review against `README.md`, `docs\README.md`, and setup scripts recorded in the execution report.

## Browser Validation Logging

- N/A - documentation-only inventory; no browser-visible behavior.

## Progression Gate

- Downstream setup/project README work may proceed only after the inventory and source references are recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
