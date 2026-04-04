# 02 - Context Menu, Links, And Dependencies

## Status

- `Completed`

## Objective

- Validate the higher-risk interactive canvas behaviors the user explicitly requested: right-click menus, canvas features, links, and dependencies.

## Covered Inputs

- `RQ-03`
- `RQ-04`

## Prerequisites

- `01-mcp-canvas-harness-and-core-node-coverage` must be completed or honestly blocked before this phase starts.

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\SharedCanvasBrowserTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureQuickActions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`

## Deliverables

- real MCP proof for right-click and canvas context menus
- real MCP proof for links and dependency interactions
- screenshots of open-menu and post-action states

## Dependency Impact

- `03` depends on this phase because any discovered failures must be repaired against exact reproduced evidence.

## Validation Depth

- critical interaction coverage

## Implementation Steps

1. Exercise right-click context menus and verify open-state layout, clipping, and layering.
2. Exercise relevant canvas features such as quick actions or context-driven creation.
3. Create and validate links or dependency relationships through the UI.
4. Capture before and after screenshots for each high-risk interaction group.

## Do Not Do

- Do not validate menus only in the closed state.
- Do not call links or dependencies covered without a visible browser assertion.

## Acceptance Checklist

- Right-click menus open correctly and remain readable.
- Canvas context actions execute and leave the expected visible state.
- At least one link or dependency flow is proven end-to-end.

## Proof Required

- MCP interaction log
- screenshots for open-menu and post-action states
- execution-report analytics row

## Browser Validation Logging

- target route: `/projects/{ProjectId}/structure`
- required viewport passes: `1600x1000`, then `1100x900`
- required Playwright MCP evidence: right-click, menu selection, link or dependency action, and screenshot capture
- expected screenshot location: `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\`

## Progression Gate

- `03` may start only after the high-risk interaction sweep is either green or tied to an explicit failing repair scope.

## Suggested Agent Prompt

```text
Implement this subbundle only. Validate right-click menus, context actions, links, and dependencies on the structure canvas through a real Playwright MCP browser session.
```
