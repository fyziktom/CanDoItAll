# Implementation Prompt — RecommendationOverlay

Implement `RecommendationOverlay` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Prompt Factory domain components`
- Status today: `missing`
- Priority: `P2`
- Scope: `domain-specific`
- JS bridge: `partial`

## Objective

Display AI-driven or rules-driven recommendations directly on the Prompt Factory canvas as contextual badges, callouts, or action popovers.

## Required behavior

- Show suggested next blocks or missing inputs near a selected prompt node.
- Present accept/reject actions for recommendations directly on the canvas.
- Drive future onboarding or best-practice guidance in the editor.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`

## Recommended implementation locations

- `src/CanDoItAll.Modules.Factory/CanvasAdapters/RecommendationOverlay.cs`
- `src/CanDoItAll.Modules.Factory/wwwroot/js/recommendation-overlay.js`
- `tests/CanDoItAll.Tests.Components/RecommendationOverlayTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `RecommendationOverlay` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `RecommendationOverlay` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
