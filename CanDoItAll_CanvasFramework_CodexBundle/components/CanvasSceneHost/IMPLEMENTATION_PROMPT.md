# Implementation Prompt — CanvasSceneHost

Implement `CanvasSceneHost` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Utility and infrastructure components`
- Status today: `partial`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Provide a unified lifecycle host for canvas-family surfaces: mount, update, resize, theme sync, overlay slots, disposal, and diagnostics hooks.

## Required behavior

- Mount the graph workbench inside a Blazor page and forward typed callbacks to C#.
- Mount the calendar runtime inside the same shell conventions without re-implementing create/update/dispose patterns.
- Expose a stable host handle for diagnostics, overlay layers, resize observers, and test hooks.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430`
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/CanvasSceneHost.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvas-scene-host.js`
- `tests/CanDoItAll.Tests.Components/CanvasSceneHostTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CanvasSceneHost` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CanvasSceneHost` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
