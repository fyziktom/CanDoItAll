# Current-state analysis

## Verified findings

1. The shared floating window component rendered literal text actions: `Open`, `Min`, `Reset`, and `Hide`.
2. The shared canvas create composer rendered as one long card with the action row at the bottom of the content stack, so long forms could push the submit button below the visible area.
3. The project structure toolbox reused shared toolbox styles that assume a two-column body. The page markup only supplied one column, which wasted width and contributed to the bad scroll behavior.
4. Existing Playwright flows depend on stable composer selectors such as `.cw-canvas-composer__input`, `.cw-canvas-composer__textarea`, and `.cw-canvas-composer__actions`.

## Implementation constraint

The user asked for a wizard-style create surface, but a hard next/back wizard would have created unnecessary regression risk across the current browser suite. The correct minimal change was a sectioned wizard shell with explicit steps, internal scrolling, and a persistent action bar while preserving the existing field selectors and DOM order.

## Affected code paths

- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`
- `src/CanDoItAll.Components.BaseLib/Components/Identity/FontAwesomeIconCatalog.cs`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/canvas-workbench.css`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
