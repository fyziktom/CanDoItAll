# CanvasWorkbenchStageShell

CanvasWorkbenchStageShell is a P0 shared high-level component in the category `Layout and navigation components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Layout and navigation components |
| Status | exists |
| Priority | P0 |
| Level | high-level |
| Scope | shared |
| JS bridge | none |
| Implementation wave | Wave 2 |

## Purpose

Shared stage layout that wraps the canvas shell with eyebrow/title copy, stats, inspector area, and supporting panel zones.

## Why this component is needed

This stage shell already unifies page composition language between Project Structure and Prompt Factory.

## Main use cases

- Render left canvas and right inspector layout.
- Expose lower supporting panels and custom toolbar slots.
- Provide a consistent product-family stage frame for future editors.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

CanvasWorkbenchStageShell already exists in the repository and should be treated as the extraction point, not replaced by a parallel implementation.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor`
- `src/CanDoItAll.ComponentKit/Canvas/Graph/CanvasWorkbenchStageShell.cs`
- `tests/CanDoItAll.Tests.Components/CanvasWorkbenchStageTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82` — Shared shell used by Project Structure and Prompt Factory for eyebrow/title copy, stats, canvas slot, inspector slot, and supporting panels. Key symbols: CanvasWorkbenchStage.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....

## Related components

- CanvasWorkbenchShell
- CanvasThemeTokenPack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CanvasWorkbenchStageShell` boundary in the recommended target path(s).
3. Preserve the current public usage surface where possible. Refactor internals, split responsibilities, and add tests instead of forcing a broad page-level API rewrite.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Preserve the current public usage surface where possible. Refactor internals, split responsibilities, and add tests instead of forcing a broad page-level API rewrite.
