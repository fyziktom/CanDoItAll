# Implementation Prompt — HoverFocusRouter

Implement `HoverFocusRouter` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Interactive components`
- Status today: `partial`
- Priority: `P1`
- Scope: `shared`
- JS bridge: `required`

## Objective

Coordinate hover, focus, and active-target visuals across nodes, connectors, menus, and editor overlays.

## Required behavior

- Highlight a hovered node while suppressing stale hover when a context menu opens.
- Transfer focus between canvas node, inline editor, and floating inspector without losing semantic selection.
- Prepare for tooltip/popover timing and handle hover-intent logic.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/HoverFocusRouter.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/hover-focus-router.js`
- `tests/CanDoItAll.Tests.Components/HoverFocusRouterTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `HoverFocusRouter` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `HoverFocusRouter` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
