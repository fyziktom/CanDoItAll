# Implementation Prompt — TextMeasureService

Implement `TextMeasureService` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Text components`
- Status today: `missing`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Provide shared text measurement, wrapping, truncation, ellipsis, multi-line sizing, and font-cache behavior for graph and calendar surfaces.

## Required behavior

- Measure node titles and subtitles before final card layout.
- Apply consistent multi-line wrapping and ellipsis rules to calendar events and graph cards.
- Support icon+text chips and badges without repeated browser measurement code.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/TextMeasureService.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/text-measure-service.js`
- `tests/CanDoItAll.Tests.Components/TextMeasureServiceTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `TextMeasureService` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `TextMeasureService` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
