# Make project structure toolbox groups behave like real accordions

## Status

- `Completed`

## Objective

- Make the toolbox behave like an obvious accordion in the default desktop layout so group headers are directly clickable, opened groups reveal their items, and those items remain usable for creating nodes.

## Covered Inputs

- `R001`
- `R002`
- `R003`
- Raw notes `N001`, `N002`, `N003`, `N004`
- Live finding: the health floating window currently overlaps the toolbox and intercepts clicks.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- Default layout no longer lets another floating window block toolbox group headers.
- Toolbox groups read and behave like an accordion on desktop.
- Opened groups clearly display their child items.
- A real browser pass proves an item can still be created from an opened group.

## Implementation Steps

1. Inspect the toolbox and floating-window layout code path that decides the initial positions and z-order.
2. Adjust the default placement or stacking so the toolbox remains unobstructed on first render.
3. Refine toolbox markup or CSS only if needed to make the accordion structure more obvious without breaking search expansion.
4. Add or update component tests for the affected state or rendered structure.
5. Validate in the real app with Playwright MCP and capture the required screenshots.

## Scope Exceptions

- Do not solve selection-panel duplication or file badge semantics here unless a change is strictly required to keep the toolbox fix working.

## Do Not Do

- Do not introduce a new floating-window system.
- Do not change toolbox search behavior.
- Do not restructure unrelated tool windows.

## Acceptance Checklist

- The toolbox is fully visible and group headers are directly clickable on the default project-structure route.
- Clicking a closed group opens it and reveals its child items.
- Clicking another group updates the accordion state without breaking search behavior.
- A node can be created from an item in an opened group.

## Proof Required

- Browser pass at `1600x1000`.
- Additional `1280x900` pass if floating-window layout or CSS changes affect available space.
- Screenshot of the default desktop layout with the unobstructed toolbox.
- Screenshot of an opened toolbox group showing visible child items.
- Playwright proof that an item from the opened group can create a node.

## Browser Validation Logging

- Route: `http://127.0.0.1:5188/projects/{id}/structure`
- Viewports: `1600x1000`, plus `1280x900` if layout changes require a narrower follow-up
- Required Playwright MCP actions:
- Open the structure page.
- Verify the toolbox window is visible without dismissing another window first.
- Click at least two group headers.
- Verify child items appear under the active group.
- Create one node from the opened group and verify it appears on the canvas.
- Required screenshots:
- `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-01-toolbox-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-01-toolbox-open-group.png`
- `C:\repositories\CanDoItAll\output\playwright\feedback8\subbundle-01-toolbox-narrow.png` when the narrower pass is used

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Fix the default toolbox interaction path so the accordion is visible and operable without another floating window intercepting clicks. Preserve existing search behavior, prove node creation from an opened group in the real browser, and record the required analytics row and screenshot paths in the execution report before closing the subbundle.
```
