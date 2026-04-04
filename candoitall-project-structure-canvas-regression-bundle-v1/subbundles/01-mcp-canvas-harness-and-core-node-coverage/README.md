# 01 - MCP Harness And Core Node Coverage

## Status

- `Completed`

## Objective

- Prove that Playwright MCP is usable again in the elevated session, bring up a local app target, and validate core node-creation flows on the structure canvas.

## Covered Inputs

- `RQ-01`
- `RQ-02`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\ProjectStructureArtifactBrowserTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`

## Deliverables

- working MCP browser session against a live local app
- seeded or freshly created project suitable for canvas testing
- real browser proof for adding multiple node types on the structure canvas

## Dependency Impact

- `02` depends on this phase because it needs a trusted live browser session and a stable canvas target.

## Validation Depth

- critical UI foundation

## Implementation Steps

1. Prove direct Playwright MCP navigation works in the elevated session.
2. Launch a local app target and reach the structure canvas.
3. Create or prepare a disposable project for regression work.
4. Add multiple node types through the canvas UI and capture screenshots plus assertions.

## Do Not Do

- Do not treat runner-based Playwright tests as a substitute for MCP proof in this phase.
- Do not proceed to later phases on a weak or unstable browser target.

## Acceptance Checklist

- Playwright MCP successfully navigates and interacts with the live app.
- The structure canvas is open in a maximized or large-screen viewport.
- Multiple node types are added successfully through the UI.

## Proof Required

- direct MCP snapshot and interaction log
- large-screen screenshot set
- execution-report analytics row

## Browser Validation Logging

- target route: `/projects`, then `/projects/{ProjectId}/structure`
- required viewport passes: `1600x1000` or larger, then `1100x900`
- required Playwright MCP evidence: navigation, selection, creation interactions, and screenshot capture
- expected screenshot location: `C:\repositories\CanDoItAll\output\playwright\canvas-regression-v1\`

## Progression Gate

- `02` may start only after the live MCP session is stable and the node-creation baseline is green.

## Suggested Agent Prompt

```text
Implement this subbundle only. Prove Playwright MCP works in the elevated session, open the structure canvas on a disposable project, and validate broad node creation through real UI interactions.
```
