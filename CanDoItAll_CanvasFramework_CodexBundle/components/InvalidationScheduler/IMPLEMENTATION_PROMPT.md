# Implementation Prompt — InvalidationScheduler

Implement `InvalidationScheduler` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Utility and infrastructure components`
- Status today: `missing`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Centralize redraw invalidation, requestAnimationFrame batching, dirty-region coalescing, and state publish throttling.

## Required behavior

- Batch connector recalculation after multiple node moves.
- Delay expensive measurements until the next animation frame.
- Coalesce viewport, selection, and layout updates into a single refresh tick.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/InvalidationScheduler.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/invalidation-scheduler.js`
- `tests/CanDoItAll.Tests.Components/InvalidationSchedulerTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `InvalidationScheduler` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `InvalidationScheduler` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
