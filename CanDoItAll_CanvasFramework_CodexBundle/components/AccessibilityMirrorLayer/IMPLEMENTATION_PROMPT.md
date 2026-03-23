# Implementation Prompt — AccessibilityMirrorLayer

Implement `AccessibilityMirrorLayer` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Utility and infrastructure components`
- Status today: `missing`
- Priority: `P2`
- Scope: `shared`
- JS bridge: `required`

## Objective

Maintain a hidden but semantic DOM representation of interactive canvas content for screen readers and keyboard navigation fallbacks.

## Required behavior

- Expose selected node summaries and actionable items to assistive tech.
- Mirror calendar event selection and navigation outside the visual canvas.
- Provide keyboard-only navigation through scene entities when direct canvas semantics are insufficient.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/AccessibilityMirrorLayer.cs`
- `src/CanDoItAll.ComponentKit/Components/AccessibilityMirrorLayer.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/accessibility-mirror-layer.js`
- `tests/CanDoItAll.Tests.Components/AccessibilityMirrorLayerTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `AccessibilityMirrorLayer` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `AccessibilityMirrorLayer` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
