# Implementation Prompt — DiagnosticsOverlay

Implement `DiagnosticsOverlay` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Diagnostic and developer components`
- Status today: `missing`
- Priority: `P1`
- Scope: `shared`
- JS bridge: `required`

## Objective

Surface debug information for scene bounds, layer order, hit regions, selection, frame timing, and invalidation reasons.

## Required behavior

- Toggle overlays that show node bounds, connector anchors, and selection rectangles.
- Display frame timing and dirty-layer counters during drag and zoom tuning.
- Enable QA and future Codex agents to validate event routing and visual bounds without reverse-engineering DOM internals.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `docs/canvases-improvements/04-implementation-plan.md#L1-L334`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Diagnostics/DiagnosticsOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/DiagnosticsOverlay.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/diagnostics-overlay.js`
- `tests/CanDoItAll.Tests.Components/DiagnosticsOverlayTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `DiagnosticsOverlay` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `DiagnosticsOverlay` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
