# Implementation Prompt — CalendarMiniMonthNavigator

Implement `CalendarMiniMonthNavigator` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Calendar domain components`
- Status today: `partial`
- Priority: `P2`
- Scope: `shared`
- JS bridge: `required`

## Objective

Provide the mini-month or small navigation calendar used to jump between dates and scopes.

## Required behavior

- Jump to dates from the project calendar side panel.
- Sync visible date selection with the main calendar view.
- Support future compact calendar dashboards or embedded mini navigators.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarMiniMonthNavigator.cs`
- `src/CanDoItAll.ComponentKit/Components/CalendarMiniMonthNavigator.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-mini-month-navigator.js`
- `tests/CanDoItAll.Tests.Components/CalendarMiniMonthNavigatorTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CalendarMiniMonthNavigator` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CalendarMiniMonthNavigator` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
