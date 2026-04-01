# runtime-keyboard-navigation-and-menu-affordances

## Status

- `Completed`

## Objective

- Implement keyboard-driven context-menu navigation, visible shortcut affordances, and the focused maintainability extraction needed to keep the runtime manageable.

## Covered Inputs

- `N001` Simplify menu orientation from the keyboard.
- `N002` Single-letter shortcuts should select menu items.
- `N003` First key on an open menu should open the matching second-layer menu.
- `N006` Preserve runtime behavior for meetings, people, infrastructure, note, and work flows.
- `N009` Underscore the shortcut letter in menu labels.
- `N010` Split `03-interaction-and-state.js` if practical for maintainability.

## Prerequisites

- `01-shortcut-contract-and-catalog-foundation` complete with passing proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\05-viewport-and-events.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`
- `C:\repositories\CanDoItAll\tools\canvaslib\asset-manifest.json`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`

## Deliverables

- Keyboard routing for printable keys when the context menu is open.
- Nested submenu progression driven by the current menu layer.
- Visual underline or equivalent emphasis for the effective shortcut letter in textual labels.
- Accessible naming that reflects the effective shortcut.
- Focused runtime extraction if needed to keep `03-interaction-and-state.js` from growing further.

## Dependency Impact

- `03-help-modal-information-architecture-and-shortcut-docs` depends on the actual runtime behavior and visible shortcut hints being correct.
- `04-browser-proof-and-closure` depends on this phase for both browser routing truth and route-load confidence after any runtime extraction.
- Weak proof here would make later screenshots and help guidance untrustworthy.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add a runtime helper for reading accelerator metadata from the current menu layer.
2. Route printable key presses into the open context menu without stealing editable-field input or breaking existing global shortcuts.
3. Open child menus on matching accelerator keys and execute leaf actions on matching leaf keys.
4. Render a visible shortcut emphasis inside menu labels and align accessible names with the effective key.
5. Extract shortcut-heavy helpers from `03-interaction-and-state.js` into a focused runtime file if that reduces maintenance risk without breaking load order.
6. Update route assets or manifest entries if a new runtime file is introduced.
7. Capture focused browser proof before allowing downstream help work to proceed.

## Scope Exceptions

- Do not finalize help-modal documentation in this phase.
- Do not broaden the runtime refactor into unrelated selection or viewport rewrites.

## Do Not Do

- Do not listen for shortcuts when the context menu is closed.
- Do not override `Escape`, zoom, help, or editable control behavior.
- Do not document shortcut pages before the runtime behavior is proven.

## Acceptance Checklist

- Open context menu responds to matching single-letter keys in the active layer.
- Nested keyboard flow works through at least second-layer and third-layer menus where children exist.
- Shortcut underline matches the actual key assignment.
- Accessible menu labeling exposes the shortcut.
- Any new runtime file loads correctly on the route.

## Proof Required

- Focused automated tests for any new helper behavior where practical.
- Browser proof on `/projects/{projectId}/structure` showing:
  - one submenu-opening shortcut path
  - one leaf-executing shortcut path
  - visible underline or emphasis for the active shortcut
- Screenshots at `1600x1000` and `1280x800`.
- Execution-report entries for commands, screenshots, and observed keyboard paths.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewports: `1600x1000` first, then `1280x800`
- Playwright actions: open the context menu, send matching keys for top-layer and nested items, verify submenu state, verify a leaf action path, inspect rendered label emphasis
- Screenshot targets:
  - `evidence/context-menu-shortcuts-desktop.png`
  - `evidence/context-menu-shortcuts-narrow.png`
- Review questions:
  - Does the documented key open the expected submenu or leaf?
  - Is the emphasized character visually obvious and correct?
  - Does the menu remain readable in compact nested layouts?

## Progression Gate

- Downstream subbundles may continue only after browser proof confirms keyboard routing, nested progression, shortcut emphasis, and route-load stability.

## Suggested Agent Prompt

```text
Implement only subbundle 02 for the canvas context-menu shortcuts bundle.
Use the shared accelerator metadata from subbundle 01 to drive open-menu keyboard routing, nested submenu progression, visible shortcut emphasis, and accessible menu labeling.
Keep the shortcut scope limited to the open context menu, preserve global shortcuts and editable fields, and make only a focused maintainability extraction from 03-interaction-and-state.js if needed.
Capture browser proof and screenshots before closing the phase.
```
