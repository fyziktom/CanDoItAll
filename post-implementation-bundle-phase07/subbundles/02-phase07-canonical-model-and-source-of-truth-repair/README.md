# Phase07 canonical model and source-of-truth repair

## Status

- `Blocked`

## Objective

- Reopen only if later evidence shows the process MCP introduced duplicate process-definition or runtime models, or bypassed canonical application services.

## Covered Inputs

- `N11`
- `REQ-023`

## Prerequisites

- `C:\repositories\CanDoItAll\process-management-bundle\subbundles\26-process-local-mcp-server-and-tool-contracts\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\reviews\01-execution-report.md`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\ProcessToolModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Processes\ProcessesTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`

## Deliverables

- Explicit repair work only if single-source-of-truth drift is discovered.

## Dependency Impact

- Weak proof here would let the MCP and process module diverge immediately.

## Validation Depth

- `Critical canonical-model closure`

## Implementation Steps

1. Review the phase07 source and proof.
2. Keep this lane blocked unless canonical-model drift appears.

## Scope Exceptions

- `N/A`

## Do Not Do

- Do not normalize duplicate process models into "temporary" compatibility layers.

## Acceptance Checklist

- The lane remains blocked while canonical process services remain the source of truth.

## Proof Required

- Root bundle build, test, and source review evidence.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Stay blocked unless a real canonical-model defect appears.

## Suggested Agent Prompt

```text
Reopen this lane only if the process MCP starts duplicating process-domain truth.
```
