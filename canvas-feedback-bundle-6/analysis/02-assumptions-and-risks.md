# Assumptions And Risks

## Assumptions

- The safest implementation keeps the current action catalog shape and reworks rendering and submenu geometry in shared CanvasLib code.
- A dedicated browser test is the correct proof vehicle because submenu delay and toolbar-safe placement are interaction-driven.
- The loading-circle indicator can reuse the existing progress-ring visual language instead of introducing a separate spinner system.

## Risks

- Geometry changes in shared `canvasWorkbenchInterop.js` can affect other radial menus beyond project structure.
- Larger hexes plus toolbar-safe clamping can force more aggressive submenu repositioning near viewport edges.
- Hover-delay logic can feel broken if pointer leave, submenu reopen, and nested layer cleanup are not synchronized carefully.

## Risk Handling

- Keep menu metric changes scoped to progress, marker, and priority preset variants.
- Add focused browser assertions for overlap absence, delay visibility, and toolbar-safe placement before closing the bundle.
- Preserve category grouping and existing action ids so menu semantics stay stable while the layout changes.
