# Implementation Prompt — CanvasWorkbenchStageShell

Implement `CanvasWorkbenchStageShell` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Layout and navigation components`
- Status today: `exists`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `none`

## Objective

Shared stage layout that wraps the canvas shell with eyebrow/title copy, stats, inspector area, and supporting panel zones.

## Required behavior

- Render left canvas and right inspector layout.
- Expose lower supporting panels and custom toolbar slots.
- Provide a consistent product-family stage frame for future editors.

## Reuse / refactor directives

- Preserve the current public usage surface where possible. Refactor internals, split responsibilities, and add tests instead of forcing a broad page-level API rewrite.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor`
- `src/CanDoItAll.ComponentKit/Canvas/Graph/CanvasWorkbenchStageShell.cs`
- `tests/CanDoItAll.Tests.Components/CanvasWorkbenchStageTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CanvasWorkbenchStageShell` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CanvasWorkbenchStageShell` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
