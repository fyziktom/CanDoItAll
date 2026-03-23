# Implementation Prompt — ProjectCalendarAdapter

Implement `ProjectCalendarAdapter` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Calendar domain components`
- Status today: `missing`
- Priority: `P0`
- Scope: `domain-specific`
- JS bridge: `none`

## Objective

Map project-specific calendar domain models and view-state persistence to the shared CanvasCalendar contract.

## Required behavior

- Map ProjectCalendarSurface and ProjectCalendarEvent to CanvasCalendarSurface and CanvasCalendarEvent.
- Persist view state through ProjectWorkbenchService using typed calendar state objects.
- Hide legacy wrapper details from the page.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161`
- `src/CanDoItAll.Modules.Workbench/Components/ProjectEventsCalendar.razor#L1-L79`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`
- `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor#L1-L258`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`

## Recommended implementation locations

- `src/CanDoItAll.Modules.Workbench/Calendar/ProjectCalendarAdapter.cs`
- `tests/CanDoItAll.Tests.Components/ProjectCalendarAdapterTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `ProjectCalendarAdapter` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `ProjectCalendarAdapter` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
