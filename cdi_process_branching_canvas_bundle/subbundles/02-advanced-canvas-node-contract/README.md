# Advanced Canvas Node Contract

## Status

- `Completed`

## Objective

- Add the shared optional multi-port node and port-aware link contract to CanvasLib without regressing existing legacy workbench nodes.

## Covered Inputs

- `N003` One curve per matched output plus default and error.
- `N006` Add an optional advanced node type and do not change old ones.
- `N007` Match the screenshot-style multi-port visual direction.

## Prerequisites

- `subbundles/01-scenario-definition-and-live-gap-reconciliation` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchNode.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchSurface.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06a-canvas-scene-and-hit-testing.js`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ConnectorAnchorOverlayTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ConnectorPathPrimitiveTests.cs`

## Deliverables

- Additive advanced-node and advanced-link contract with stable port identifiers.
- Port-aware rendering and hit testing with fallback to current whole-node behavior.
- Regression tests proving legacy nodes still render and advanced nodes route by port.

## Dependency Impact

- Every user-visible branch-node behavior depends on this phase.
- If the shared contract or fallback behavior is wrong, all later process-workspace screenshots become untrustworthy.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add the minimal additive C# contract for advanced workbench nodes and links.
2. Thread the contract through the shared workbench component and renderer state.
3. Update anchor and path calculations so links prefer named-port geometry when present.
4. Add or update shared-component tests for port geometry and legacy fallback.
5. Run a dependent browser smoke on `/processes` before closing the subbundle.

## Scope Exceptions

- Do not implement process-specific branch-node creation here.

## Do Not Do

- Do not rewrite legacy nodes into advanced nodes by default.
- Do not hardcode process concepts such as branch outcomes into CanvasLib.

## Acceptance Checklist

- Legacy canvases still render with existing node and link models.
- Advanced nodes can define more than one input or output with stable identifiers.
- Shared renderer and hit testing can locate specific port anchors.
- Browser smoke shows the process workspace still loads after the shared changes.

## Proof Required

- Focused component tests covering advanced-port geometry and legacy fallback.
- One browser smoke on `/processes` at `1600x900` with a screenshot proving no immediate workbench regression.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `1600x900`
- Playwright MCP actions: navigate, wait for workspace, inspect rendered canvas shell, capture screenshot
- Expected evidence path: a desktop screenshot recorded in `reviews/01-execution-report.md`
- Screenshot review questions: does the existing workbench still render cleanly, is any legacy node clipped, and is there any obvious connector regression

## Progression Gate

- Subbundle `03` may continue only after shared tests pass and the browser smoke shows the workspace still renders without legacy-canvas regressions.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add an optional multi-port workbench node and port-aware link contract in CanvasLib, keep the legacy path intact, add the minimum shared tests, and prove the workspace still renders in the browser before moving on.
```

## Closure Notes

- Focused shared-component tests passed after the additive port contract and renderer changes landed.
- The `/processes` workspace rendered cleanly after the shared changes, with no browser-console errors during the proof pass.
