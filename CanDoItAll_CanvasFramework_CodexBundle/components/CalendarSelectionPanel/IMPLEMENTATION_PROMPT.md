# Implementation Prompt — CalendarSelectionPanel

Implement `CalendarSelectionPanel` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Calendar domain components`
- Status today: `partial`
- Priority: `P1`
- Scope: `shared`
- JS bridge: `required`

## Objective

Present selected event details, contextual playlists/checklists, and selection-derived actions in the calendar shell.

## Required behavior

- Show selected event details and connected data in the project calendar.
- Surface playlist or checklist actions through typed callbacks.
- Support future plug-in side panels for event metadata.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarSelectionPanel.cs`
- `src/CanDoItAll.ComponentKit/Components/CalendarSelectionPanel.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-selection-panel.js`
- `tests/CanDoItAll.Tests.Components/CalendarSelectionPanelTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CalendarSelectionPanel` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CalendarSelectionPanel` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
