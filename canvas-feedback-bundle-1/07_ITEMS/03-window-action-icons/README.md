# Item 03: Window action icons

## Covered notes

- `N003`
- `N004`
- `N005`
- `N006`

## Scope

- Replace floating window text buttons with icons only.
- Route the requested icon tokens through the shared icon catalog.
- Keep accessibility labels explicit.

## Implemented in

- `src/CanDoItAll.Components.BaseLib/Components/Identity/FontAwesomeIconCatalog.cs`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/canvas-workbench.css`
- `tests/CanDoItAll.Tests.Components/CanvasFloatingWindowTests.cs`

## Follow-up

- Browser visibility, black icon color, and screenshot evidence are captured in `Item 04`.

## Status

`Implemented; browser visibility closure completed later`
