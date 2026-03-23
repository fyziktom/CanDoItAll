# Implementation Prompt — ProjectCalendarStateParser

Implement `ProjectCalendarStateParser` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Calendar domain components`
- Status today: `missing`
- Priority: `P1`
- Scope: `domain-specific`
- JS bridge: `none`

## Objective

Parse and normalize persisted project calendar view state into a typed model, replacing manual JSON probing in the page.

## Required behavior

- Read selected event ID, preferred view, visible date, and scope from persisted JSON.
- Provide defaults when no state exists or the schema is older than current expectations.
- Support future migration of view-state shape without page-level hacks.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`

## Recommended implementation locations

- `src/CanDoItAll.Modules.Workbench/Calendar/ProjectCalendarStateParser.cs`
- `tests/CanDoItAll.Tests.Components/ProjectCalendarStateParserTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `ProjectCalendarStateParser` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `ProjectCalendarStateParser` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
