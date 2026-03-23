# SkeletonStateOverlay

SkeletonStateOverlay is a P2 shared high-level component in the category `Overlay, inspector, and helper components`.

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

Provide loading skeletons for workbench cards, toolbar chrome, and inspector placeholders before real scene data arrives.

## Why this component is needed

Canvas surfaces often show data-dependent content, but there is no shared skeleton strategy for them today.

## Main use cases

- Show a coherent loading frame while graph nodes are loading.
- Mask small async refreshes of side panels without layout jumps.
- Support future optimistic loading states during scene swaps.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

SkeletonStateOverlay is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/SkeletonStateOverlay.cs`
- `src/CanDoItAll.ComponentKit/Components/SkeletonStateOverlay.razor`
- `tests/CanDoItAll.Tests.Components/SkeletonStateOverlayTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82` — Shared shell used by Project Structure and Prompt Factory for eyebrow/title copy, stats, canvas slot, inspector slot, and supporting panels. Key symbols: CanvasWorkbenchStage.
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.

## Related components

- CanvasWorkbenchStageShell
- ContainerPrimitive
- AnimationTimeline

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `SkeletonStateOverlay` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
