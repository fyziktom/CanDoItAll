# EmptyStateOverlay

EmptyStateOverlay is a P2 shared high-level component in the category `Overlay, inspector, and helper components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Overlay, inspector, and helper components |
| Status | missing |
| Priority | P2 |
| Level | high-level |
| Scope | shared |
| JS bridge | none |
| Implementation wave | Wave 6 |

## Purpose

Render meaningful empty-state overlays when a canvas surface has no content or no valid projection.

## Why this component is needed

Empty surfaces will become more common as framework reuse grows; explicit empty-state handling is currently missing.

## Main use cases

- Show Project Structure onboarding for a blank project graph.
- Show Prompt Factory guidance when no canvas nodes are yet projected.
- Provide fallback when filters hide all items.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

EmptyStateOverlay is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/EmptyStateOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/EmptyStateOverlay.razor`
- `tests/CanDoItAll.Tests.Components/EmptyStateOverlayTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82` — Shared shell used by Project Structure and Prompt Factory for eyebrow/title copy, stats, canvas slot, inspector slot, and supporting panels. Key symbols: CanvasWorkbenchStage.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `docs/canvases-improvements/01-reference-and-gap-analysis.md#L1-L203` — Existing internal analysis of reference workbench behavior and gaps. Key symbols: Reference capability inventory, Page shell and layout, Canvas host and chrome.

## Related components

- CanvasWorkbenchStageShell
- CreateActionPalette
- TextBlockPrimitive
- IconGlyphPrimitive

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `EmptyStateOverlay` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
