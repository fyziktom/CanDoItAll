# 03 - Conditional Repairs And Closure

## Status

- `Completed`

## Objective

- Repair any failures found in the regression sweep, rerun the exact MCP proof, and close the bundle with honest browser analytics.

## Covered Inputs

- `RQ-05`
- `RQ-06`

## Prerequisites

- `01-mcp-canvas-harness-and-core-node-coverage` must be completed or honestly blocked before this phase starts.
- `02-context-menu-links-and-dependencies` must be completed or honestly blocked before this phase starts.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`
- `C:\repositories\CanDoItAll\candoitall-project-structure-canvas-regression-bundle-v1\reviews\01-execution-report.md`

## Deliverables

- scoped repair work for any reproduced failures
- rerun browser proof for repaired flows
- completed bundle closure with final validator output

## Dependency Impact

- no downstream subbundles remain

## Validation Depth

- end-to-end closure

## Implementation Steps

1. If a failure exists, scope the smallest correct repair and implement it.
2. Rerun the exact failing MCP flow until it passes cleanly.
3. Update the execution report, raw-note closure, and browser analytics.
4. Run the completed-stage validator and close the bundle.

## Do Not Do

- Do not leave a reproduced failure sitting in residual risk.
- Do not close the bundle without rerunning the exact repaired flow.

## Acceptance Checklist

- every reproduced failure is either repaired and rerun or honestly blocked
- browser analytics match the actual executed flows
- completed-stage validator passes

## Proof Required

- code diff if repairs were needed
- rerun MCP screenshots and assertions
- completed-stage validator output

## Browser Validation Logging

- target routes: affected live routes from earlier subbundles
- required viewport passes: same as the failing proof plus any narrower follow-up needed
- required Playwright MCP evidence: rerun of the exact failing interaction
- expected screenshot location: `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\`

## Progression Gate

- the bundle may close only after every executed subbundle has honest proof and the completed validator passes.

## Suggested Agent Prompt

```text
Implement this subbundle only. Repair any reproduced canvas regression, rerun the exact Playwright MCP proof, and close the bundle honestly.
```
