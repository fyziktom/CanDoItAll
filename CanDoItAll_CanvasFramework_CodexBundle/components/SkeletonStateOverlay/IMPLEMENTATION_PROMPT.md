# Implementation Prompt — SkeletonStateOverlay

Implement `SkeletonStateOverlay` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Overlay, inspector, and helper components`
- Status today: `missing`
- Priority: `P2`
- Scope: `shared`
- JS bridge: `none`

## Objective

Provide loading skeletons for workbench cards, toolbar chrome, and inspector placeholders before real scene data arrives.

## Required behavior

- Show a coherent loading frame while graph nodes are loading.
- Mask small async refreshes of side panels without layout jumps.
- Support future optimistic loading states during scene swaps.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/SkeletonStateOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/SkeletonStateOverlay.razor`
- `tests/CanDoItAll.Tests.Components/SkeletonStateOverlayTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `SkeletonStateOverlay` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `SkeletonStateOverlay` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
