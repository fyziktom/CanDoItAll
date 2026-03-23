# Implementation Prompt — ProjectStructureGraphAdapter

Implement `ProjectStructureGraphAdapter` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Project Structure domain components`
- Status today: `partial`
- Priority: `P0`
- Scope: `domain-specific`
- JS bridge: `none`

## Objective

Project ProjectStructureNode/Link domain models into the shared graph scene contract used by CanvasWorkbench.

## Required behavior

- Map project objects to CanvasWorkbenchNode and CanvasWorkbenchLink.
- Attach project-specific metadata, action groups, chips, and inspector payload hints.
- Centralize future node-shape or card-template decisions for project structure.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326`

## Recommended implementation locations

- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructureGraphAdapterTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `ProjectStructureGraphAdapter` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `ProjectStructureGraphAdapter` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
