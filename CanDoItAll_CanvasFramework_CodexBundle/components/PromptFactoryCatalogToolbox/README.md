# PromptFactoryCatalogToolbox

PromptFactoryCatalogToolbox is a P0 domain-specific high-level component in the category `Prompt Factory domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Prompt Factory domain components |
| Status | partial |
| Priority | P0 |
| Level | high-level |
| Scope | domain-specific |
| JS bridge | none |
| Implementation wave | Wave 4 |

## Purpose

Encapsulate Prompt Factory context/create action catalogs and expose them to shared create/action UI components.

## Why this component is needed

PromptFactoryCanvasCatalog is already the seed of a domain toolbox but should be treated as a first-class adapter component.

## Main use cases

- Build session, selection, component, and input node action sets.
- Drive contextual create menus and inspector create groups.
- Provide one place for future recommendation or template actions.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Factory` bridging prompt-session state and the shared graph framework.

## Current-state summary

PromptFactoryCatalogToolbox already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptFactoryCatalogToolbox.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactoryCatalogToolboxTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645` — Prompt Factory domain create/action catalog used to populate canvas context actions and create menus. Key symbols: BuildSessionContextActions, BuildSelectionContextActions, BuildComponentNodeActions, BuildInputNodeActions.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090` — Selection graph builder and catalog behavior for the prompt factory canvas. Key symbols: BuildSelectionGraph, HandleCatalogContextActionAsync, HandleCatalogCreateActionAsync.
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....

## Related components

- CreateActionPalette
- ContextMenuHost

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `PromptFactoryCatalogToolbox` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `PromptFactoryPage` partial files so graph projection/history/toolbox behavior is consumed through the adapter boundary.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
