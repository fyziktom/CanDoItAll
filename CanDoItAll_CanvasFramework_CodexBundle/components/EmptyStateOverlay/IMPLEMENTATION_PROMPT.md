# Implementation Prompt — EmptyStateOverlay

Implement `EmptyStateOverlay` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Overlay, inspector, and helper components`
- Status today: `missing`
- Priority: `P2`
- Scope: `shared`
- JS bridge: `none`

## Objective

Render meaningful empty-state overlays when a canvas surface has no content or no valid projection.

## Required behavior

- Show Project Structure onboarding for a blank project graph.
- Show Prompt Factory guidance when no canvas nodes are yet projected.
- Provide fallback when filters hide all items.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/EmptyStateOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/EmptyStateOverlay.razor`
- `tests/CanDoItAll.Tests.Components/EmptyStateOverlayTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `EmptyStateOverlay` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `EmptyStateOverlay` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
