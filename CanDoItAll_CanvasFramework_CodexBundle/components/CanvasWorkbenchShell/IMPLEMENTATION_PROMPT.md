# Implementation Prompt — CanvasWorkbenchShell

Implement `CanvasWorkbenchShell` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Layout and navigation components`
- Status today: `exists`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Primary reusable graph-workbench component that hosts the graph runtime, toolbar affordances, zoom rail, and typed events.

## Required behavior

- Project Structure graph editing.
- Prompt Factory canvas editing.
- Future graph-like editors such as dependency maps or visual planners.

## Reuse / refactor directives

- Preserve the current public usage surface where possible. Refactor internals, split responsibilities, and add tests instead of forcing a broad page-level API rewrite.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor#L1-L572`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.ComponentKit/Canvas/Graph/CanvasWorkbenchShell.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
- `tests/CanDoItAll.Tests.Components/CanvasWorkbenchTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CanvasWorkbenchShell` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CanvasWorkbenchShell` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
