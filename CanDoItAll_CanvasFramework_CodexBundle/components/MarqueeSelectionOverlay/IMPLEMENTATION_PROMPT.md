# Implementation Prompt — MarqueeSelectionOverlay

Implement `MarqueeSelectionOverlay` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Selection and transform components`
- Status today: `partial`
- Priority: `P1`
- Scope: `shared`
- JS bridge: `required`

## Objective

Render and manage the marquee selection rectangle and intersection query for multi-selection.

## Required behavior

- Alt-drag selection in the shared workbench.
- Potential future touch-lasso or box-select gestures.
- Debugging of intersection behavior and future selection filters.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/MarqueeSelectionOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/MarqueeSelectionOverlay.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/marquee-selection-overlay.js`
- `tests/CanDoItAll.Tests.Components/MarqueeSelectionOverlayTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `MarqueeSelectionOverlay` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `MarqueeSelectionOverlay` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
