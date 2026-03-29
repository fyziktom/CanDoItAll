# P0-01 Overlay Input Isolation And Wheel Ownership

## Status
- Lifecycle status: `Ready`

## Objective
- Make floating windows, toolbox content, dialogs, popovers, and support overlays fully own pointer, wheel, focus, and context-menu interaction.

## Covered Inputs
- Original request to execute the prepared bundle with real Playwright proof.
- Audit hotspot findings about overlay leakage and wheel ownership.
- Feature preservation items `F01`, `F02`, `F03`, `F04`, `F06`, `F07`, `F08`, `F09`, `F12`, `F13`, `F22`, `F23`, `F33`, and `F34`.

## Prerequisites
- None.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasFloatingWindow.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasWorkbenchTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables
- Overlay target detection that treats toolbox, floating windows, dialogs, and toolbar-safe overlays as event owners.
- Wheel routing that lets overlay scroll regions consume wheel input without zooming the scene.
- Browser-proof that overlay clicks and context menus no longer leak into the canvas host.

## Dependency Impact
- Critical foundation for every later UI subbundle because browser proof is untrustworthy if overlays still leak to the scene.
- Shared-canvas change, so PromptFactory and Sandbox regression checks are required before downstream work.

## Validation Depth
- Targeted component tests for shared canvas chrome.
- Playwright proof on ProjectStructure plus PromptFactory shared-canvas smoke.
- Screenshot review for open toolbox and other overlay states touched by the fix.

## Implementation Steps
- Audit current overlay guard logic in `canvasWorkbenchInterop.js`.
- Change only the interaction ownership paths required for wheel, pointerdown, double-click, and context menu isolation.
- Add or tighten tests for toolbox interaction and shared chrome if coverage is weak.

## Do Not Do
- Do not widen into persistence or renderer batching changes owned by later subbundles.
- Do not replace browser proof with reasoning from the code path.

## Acceptance Checklist
- Wheel inside toolbox no longer changes scene zoom.
- Clicking a toolbox accordion header never starts canvas selection or pan.
- Right-click inside toolbox or floating-window content never opens the scene context menu.
- Existing node and canvas context menus still work on the scene.

## Proof Required
- Targeted `CanvasWorkbench` component tests.
- Targeted Playwright run covering ProjectStructure overlay behavior.
- PromptFactory browser rerun because shared canvas files changed.
- Screenshots for the relevant open overlay states.

## Browser Validation Logging
- Route: ProjectStructure route plus `/prompt-factory` when shared files change.
- Viewport: large-screen first, then narrower width if layout changes.
- Record Playwright actions, assertions, screenshot paths, and the gate result in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start `P0-02` or `P0-03` until overlay leakage is proven fixed in the browser and shared-canvas smoke remains green.

## Suggested Agent Prompt
- Validate the current overlay ownership behavior first, then implement only the smallest shared-canvas change required to stop overlay wheel and context-menu leakage without breaking ProjectStructure or PromptFactory chrome.
