# Implementation Prompt — SceneNodeModel

Implement `SceneNodeModel` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Utility and infrastructure components`
- Status today: `missing`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `none`

## Objective

Define the internal scene graph contract used by shared graph components: nodes, children, bounds, transforms, visibility, hit regions, and state flags.

## Required behavior

- Represent container nodes, connectors, overlays, and decorations in one normalized graph.
- Support grouping, clipping, z-ordering, and dirty-region invalidation.
- Allow domain adapters to project domain data into a stable shared rendering model.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/SceneNodeModel.cs`
- `tests/CanDoItAll.Tests.Components/SceneNodeModelTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `SceneNodeModel` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `SceneNodeModel` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
