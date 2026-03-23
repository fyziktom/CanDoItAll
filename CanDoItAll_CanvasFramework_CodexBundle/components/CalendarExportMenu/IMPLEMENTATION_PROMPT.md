# Implementation Prompt — CalendarExportMenu

Implement `CalendarExportMenu` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Calendar domain components`
- Status today: `partial`
- Priority: `P2`
- Scope: `shared`
- JS bridge: `required`

## Objective

Present export options and package visible calendar data/context into typed export requests.

## Required behavior

- Export visible project events in different formats.
- Support future share or publish flows.
- Provide a clear UX for export scope and visible filter assumptions.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarExportMenu.cs`
- `src/CanDoItAll.ComponentKit/Components/CalendarExportMenu.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-export-menu.js`
- `tests/CanDoItAll.Tests.Components/CalendarExportMenuTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CalendarExportMenu` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CalendarExportMenu` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
