# CommandHistoryStore

CommandHistoryStore is a P1 shared low-level component in the category `Utility and infrastructure components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Utility and infrastructure components |
| Status | partial |
| Priority | P1 |
| Level | low-level |
| Scope | shared |
| JS bridge | none |
| Implementation wave | Wave 1 |

## Purpose

Provide a shared undo/redo-ready history abstraction for editor state snapshots or domain commands.

## Why this component is needed

Prompt Factory already has a page-local snapshot history. Project Structure and future canvases need the same capability without duplicating history stacks.

## Main use cases

- Undo selection-safe graph edits in Prompt Factory.
- Undo node create/move/link actions in Project Structure after shared command integration.
- Feed toolbar button enabled state and keyboard shortcut routing from one consistent source.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

CommandHistoryStore already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Core/CommandHistoryStore.cs`
- `tests/CanDoItAll.Tests.Components/CommandHistoryStoreTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234` — Prompt Factory undo/redo stack and keyboard shortcut registration. Valuable evidence for shared history and shortcut abstractions. Key symbols: UndoAsync, RedoAsync, OnAfterRenderAsync.
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536` — Prompt Factory domain models, including run node summaries, attachment summaries, setup profile, and editor state persisted with CanvasUiStateJson. Key symbols: PromptRunNodeSummary, PromptSessionAttachmentSummary, PromptSessionSetupProfile, PromptFactoryEditorModel.
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715` — Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. Key symbols: GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync, ....
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....

## Related components

- SerializationPersistencePack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CommandHistoryStore` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
