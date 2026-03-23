# Implementation Prompt — HitTestService

Implement `HitTestService` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Interactive components`
- Status today: `partial`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Provide explicit hit testing for nodes, connectors, group frames, handles, and overlay targets.

## Required behavior

- Detect pointer targets for selection, drag, context menus, and hover states.
- Support future connector anchor handles and resize/rotate handles.
- Allow minimap and marquee tools to query scene intersections consistently.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/workbenchInterop.js#L1-L884`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/HitTestService.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/hit-test-service.js`
- `tests/CanDoItAll.Tests.Components/HitTestServiceTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `HitTestService` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `HitTestService` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
