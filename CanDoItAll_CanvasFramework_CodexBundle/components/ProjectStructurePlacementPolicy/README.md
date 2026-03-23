# ProjectStructurePlacementPolicy

ProjectStructurePlacementPolicy is a P1 domain-specific high-level component in the category `Project Structure domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Project Structure domain components |
| Status | partial |
| Priority | P1 |
| Level | high-level |
| Scope | domain-specific |
| JS bridge | none |
| Implementation wave | Wave 3 |

## Purpose

Centralize placement rules for newly created project nodes relative to source node, parent node, viewport, and layout heuristics.

## Why this component is needed

ResolveCreatePlacement currently lives inside the page, making future placement behavior harder to reuse or validate.

## Main use cases

- Place a new child or sibling near the selected source object.
- Pick sensible canvas coordinates when no source node exists.
- Support future smart placement around group frames or minimap-targeted locations.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Workbench` bridging project-structure services and the shared graph framework.

## Current-state summary

ProjectStructurePlacementPolicy already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePlacementPolicyTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....

## Related components

- LayoutEngine
- ViewportController
- SnapGuideSystem

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `ProjectStructurePlacementPolicy` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `ProjectStructurePage.razor` so it consumes the adapter/policy rather than constructing graph behavior inline.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
