# Advanced Canvas Node Contract

## Status

- `Ready`

## Objective

- Refine the shared CanvasLib contract so connector authoring starts with left click on explicit connector circles, connector circles align to their badges, and legacy workbench nodes still behave correctly.

## Covered Inputs

- `N006` Add an optional advanced node type and do not change old ones.
- `N007` Match the screenshot-style multi-port visual direction.
- `N011` Left click starts connector authoring and left click confirms it on a target circle.
- `N012` Connector circles must sit exactly on their badges and none may be missing.

## Prerequisites

- `subbundles/01-scenario-definition-and-live-gap-reconciliation` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchNode.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchAnchorPorts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchEvents.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchSurface.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\overlays\05-overlays-and-composer.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07a-runtime-interaction-router.js`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ConnectorAnchorOverlayTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ConnectorPathPrimitiveTests.cs`

## Deliverables

- Shared left-click connector-authoring behavior that uses connector circles instead of right-click initiation.
- Badge-aligned connector-circle geometry for advanced and legacy-compatible node projections where badges are visible.
- Regression tests proving legacy nodes still render and advanced nodes still route by named port.

## Dependency Impact

- Every later screenshot and interaction proof depends on this phase getting the shared gesture and geometry right.
- Weak proof here would let process-specific fixes compensate locally while leaving the workbench contract inconsistent.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Audit the current pointer-routing path for connector initiation and completion.
2. Change connector authoring to left click on connector circles while protecting normal node selection and drag behavior.
3. Update anchor-geometry calculation so connector circles sit on the visible badges instead of generic edge slots.
4. Add or extend shared tests for connector targeting, badge alignment primitives, and legacy fallback behavior.
5. Run a dependent browser smoke on `/processes` before closing the subbundle.

## Do Not Do

- Do not rewrite legacy nodes into advanced nodes by default.
- Do not move process-specific semantics such as branch outcomes into CanvasLib.
- Do not solve badge alignment by hardcoding one process-node skin directly into the shared renderer.

## Acceptance Checklist

- Left click on a connector circle starts a connection draft.
- Left click on a compatible target circle completes the draft.
- Connector circles align to the badges that name their ports.
- Legacy canvases still render and behave correctly.

## Proof Required

- Focused shared-component tests covering connector interaction and geometry.
- One browser smoke on `/processes` showing the workbench still renders correctly after the shared changes.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `Large-screen desktop`
- Playwright MCP actions: navigate, inspect connector circles, initiate a draft from a circle, capture screenshot
- Expected evidence path: shared-contract screenshots recorded in `reviews/01-execution-report.md`

## Progression Gate

- `subbundles/03-process-branch-node-authoring-and-mapping` may continue only after shared tests pass and the browser smoke shows the updated left-click connector gesture without obvious legacy regressions.

## Suggested Agent Prompt

```text
Implement this subbundle only. Keep the additive CanvasLib port contract, change connector authoring to left click on explicit connector circles, align connector circles to their badges, preserve legacy behavior, add the minimum shared tests, and prove the workspace still renders in the browser before moving on.
```
