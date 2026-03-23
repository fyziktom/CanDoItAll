# Implementation Prompt — ProjectStructureActionCatalogAdapter

Implement `ProjectStructureActionCatalogAdapter` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Project Structure domain components`
- Status today: `partial`
- Priority: `P0`
- Scope: `domain-specific`
- JS bridge: `none`

## Objective

Encapsulate Project Structure create/action catalog generation and adaptation to shared action metadata.

## Required behavior

- Build contextual create menus for object types.
- Build inspector create groups and action labels.
- Keep page code free of action-tree construction details.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`

## Recommended implementation locations

- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `ProjectStructureActionCatalogAdapter` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `ProjectStructureActionCatalogAdapter` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
