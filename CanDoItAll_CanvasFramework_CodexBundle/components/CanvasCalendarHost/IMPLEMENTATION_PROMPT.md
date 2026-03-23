# Implementation Prompt — CanvasCalendarHost

Implement `CanvasCalendarHost` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Calendar domain components`
- Status today: `partial`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Primary reusable Blazor component that hosts the calendar runtime with typed event/save/delete/export callbacks.

## Required behavior

- Project calendar integration.
- Potential future calendar views in other modules.
- Export-ready typed wrapper around the specialized JS calendar engine.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `docs/canvas-events-calendar/rebuild/blazor-jsinterop-component-plan.md#L1-L186`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CanvasCalendarHost.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js`
- `tests/CanDoItAll.Tests.Components/CanvasCalendarTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CanvasCalendarHost` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CanvasCalendarHost` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
