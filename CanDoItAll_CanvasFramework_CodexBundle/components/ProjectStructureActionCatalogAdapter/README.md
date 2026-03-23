# ProjectStructureActionCatalogAdapter

ProjectStructureActionCatalogAdapter is a P0 domain-specific high-level component in the category `Project Structure domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Project Structure domain components |
| Status | partial |
| Priority | P0 |
| Level | high-level |
| Scope | domain-specific |
| JS bridge | none |
| Implementation wave | Wave 3 |

## Purpose

Encapsulate Project Structure create/action catalog generation and adaptation to shared action metadata.

## Why this component is needed

ProjectStructureCanvasCatalog is already a strong step toward reuse, but it should be formalized as a domain adapter that feeds shared create/action components.

## Main use cases

- Build contextual create menus for object types.
- Build inspector create groups and action labels.
- Keep page code free of action-tree construction details.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Workbench` bridging project-structure services and the shared graph framework.

## Current-state summary

ProjectStructureActionCatalogAdapter already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326` — Domain action catalog and label resolver for Project Structure create flows and inspector create groups. Key symbols: ResolveNodeLabel, BuildMenuCreateActions, BuildInspectorCreateGroups, TryResolveCreateDefinition.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....

## Related components

- CreateActionPalette
- ContextMenuHost

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `ProjectStructureActionCatalogAdapter` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `ProjectStructurePage.razor` so it consumes the adapter/policy rather than constructing graph behavior inline.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
