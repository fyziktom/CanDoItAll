# Implementation Prompt — ProjectStructurePlacementPolicy

Implement `ProjectStructurePlacementPolicy` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Project Structure domain components`
- Status today: `partial`
- Priority: `P1`
- Scope: `domain-specific`
- JS bridge: `none`

## Objective

Centralize placement rules for newly created project nodes relative to source node, parent node, viewport, and layout heuristics.

## Required behavior

- Place a new child or sibling near the selected source object.
- Pick sensible canvas coordinates when no source node exists.
- Support future smart placement around group frames or minimap-targeted locations.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`

## Recommended implementation locations

- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePlacementPolicyTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `ProjectStructurePlacementPolicy` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `ProjectStructurePlacementPolicy` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
