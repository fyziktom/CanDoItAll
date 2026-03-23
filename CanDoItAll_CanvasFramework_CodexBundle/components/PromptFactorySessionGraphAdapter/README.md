# PromptFactorySessionGraphAdapter

PromptFactorySessionGraphAdapter is a P0 domain-specific high-level component in the category `Prompt Factory domain components`.

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

Project Prompt Factory editor/session state into shared graph nodes, links, groups, and selection metadata.

## Why this component is needed

Prompt Factory graph construction is still in the page. An adapter is required to make the session graph explicit and reusable.

## Main use cases

- Build the session graph, selection graph, branch nodes, and run-node projections.
- Attach node kinds, labels, chips, and contextual actions.
- Persist and rehydrate canvas UI state coherently with the domain model.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Factory` bridging prompt-session state and the shared graph framework.

## Current-state summary

PromptFactorySessionGraphAdapter already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptFactorySessionGraphAdapter.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactorySessionGraphAdapterTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090` — Selection graph builder and catalog behavior for the prompt factory canvas. Key symbols: BuildSelectionGraph, HandleCatalogContextActionAsync, HandleCatalogCreateActionAsync.
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536` — Prompt Factory domain models, including run node summaries, attachment summaries, setup profile, and editor state persisted with CanvasUiStateJson. Key symbols: PromptRunNodeSummary, PromptSessionAttachmentSummary, PromptSessionSetupProfile, PromptFactoryEditorModel.
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715` — Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. Key symbols: GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync, ....
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340` — Defines the shared graph-workbench DTO layer: surface, nodes, links, UI state, chrome, actions, chips, group frames, and typed callback payloads. Key symbols: CanvasWorkbenchSurface, CanvasWorkbenchNode, CanvasWorkbenchLink, CanvasWorkbenchUiState, CanvasWorkbenchChrome, CanvasWorkbenchAction, ....

## Related components

- CanvasWorkbenchShell
- NodeCardComposer
- PromptFactoryCatalogToolbox
- PromptRunBranchLane
- PromptSessionAttachmentNode

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `PromptFactorySessionGraphAdapter` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `PromptFactoryPage` partial files so graph projection/history/toolbox behavior is consumed through the adapter boundary.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
