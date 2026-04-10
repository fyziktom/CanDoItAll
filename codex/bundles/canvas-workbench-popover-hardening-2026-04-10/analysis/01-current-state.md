# Current State

## Shared Runtime Findings

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js` calls `showPopover(state, anchorRect, hitTarget.annotation)` inside `syncSceneHoverState`, but the split file does not import, define, or late-bind `showPopover`. That makes the canvas annotation hover path vulnerable to a `ReferenceError` exactly where the reported stack points.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js` already uses a safer split-file pattern through `lateRuntime.showPopover` with a legacy fallback. The canvas-renderer path bypasses that pattern.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\05-viewport-and-events.js` initializes `hoveredNodeId`, delete-hover fields, and popover references, but does not initialize `hoveredAnnotationKey`.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\05-viewport-and-events.js` refresh logic hides the popover but does not clear the canvas annotation hover key, so rerenders can leave stale hover state behind.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js` shows the popover by writing directly to `state.popoverTitle` and `state.popoverBody` with no null or connectivity guard.
- Canvas annotation hit zones are registered in at least two rendered node paths in `06-canvas-renderers.js`, so the fix must cover shared annotation handling rather than one specific node layout.

## Consumer Surfaces

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Canvas.razor` exposes `/groups/canvas` and uses the shared `CanvasWorkbench` component directly.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` exposes `/projects/{ProjectId:guid}/structure` and is the workbench route explicitly called out by the user.
- Existing component and Playwright coverage already exists nearby, especially `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`, `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\SharedCanvasBrowserTests.cs`, and `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`.

## Initial Scope Read

- The primary defect is in shared JavaScript, not page-local Blazor code.
- The likely robustness problems are stale canvas hover state, split-file cross-reference fragility, and unsafe popover DOM assumptions.
- The repair must preserve both DOM-backed annotation badges and canvas-backed annotation hit zones.
