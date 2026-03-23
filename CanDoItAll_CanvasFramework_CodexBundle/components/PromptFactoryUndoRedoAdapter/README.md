# PromptFactoryUndoRedoAdapter

PromptFactoryUndoRedoAdapter is a P1 domain-specific high-level component in the category `Prompt Factory domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Prompt Factory domain components |
| Status | partial |
| Priority | P1 |
| Level | high-level |
| Scope | domain-specific |
| JS bridge | partial |
| Implementation wave | Wave 4 |

## Purpose

Integrate Prompt Factory editing operations with the shared CommandHistoryStore and shortcut system.

## Why this component is needed

Undo/redo exists but is isolated inside the page. It should become a domain adapter that plugs into shared history infrastructure.

## Main use cases

- Track prompt editor state snapshots after meaningful edits.
- Enable toolbar buttons and shortcuts from shared command-history state.
- Prepare for future clipboard and branch-reorder operations to participate in undo/redo.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Factory` bridging prompt-session state and the shared graph framework.

## Current-state summary

PromptFactoryUndoRedoAdapter already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptFactoryUndoRedoAdapter.cs`
- `src/CanDoItAll.Modules.Factory/wwwroot/js/prompt-factory-undo-redo-adapter.js`
- `tests/CanDoItAll.Tests.Components/PromptFactoryUndoRedoAdapterTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536` — Prompt Factory domain models, including run node summaries, attachment summaries, setup profile, and editor state persisted with CanvasUiStateJson. Key symbols: PromptRunNodeSummary, PromptSessionAttachmentSummary, PromptSessionSetupProfile, PromptFactoryEditorModel.
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715` — Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. Key symbols: GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync, ....

## Related components

- CommandHistoryStore
- KeyboardShortcutRouter
- SerializationPersistencePack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `PromptFactoryUndoRedoAdapter` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Update `PromptFactoryPage` partial files so graph projection/history/toolbox behavior is consumed through the adapter boundary.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
