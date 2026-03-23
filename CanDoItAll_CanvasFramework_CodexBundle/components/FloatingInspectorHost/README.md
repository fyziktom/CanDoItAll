# FloatingInspectorHost

FloatingInspectorHost is a P1 shared high-level component in the category `Overlay, inspector, and helper components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Overlay, inspector, and helper components |
| Status | partial |
| Priority | P1 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 2 |

## Purpose

Support detachable or floating inspector presentation anchored to the canvas stage.

## Why this component is needed

Prompt Factory already has a floating inspector helper, but it is mixed into generic workbench JS and should become a first-class shared component.

## Main use cases

- Prompt Factory floating inspector docking and persistence.
- Future compact inspector mode for smaller screens or multi-canvas workflows.
- Contextual mini inspectors near selected nodes.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

FloatingInspectorHost already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/FloatingInspectorHost.cs`
- `src/CanDoItAll.ComponentKit/Components/FloatingInspectorHost.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/floating-inspector-host.js`
- `tests/CanDoItAll.Tests.Components/FloatingInspectorHostTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37` — Floating inspector docking logic used by the prompt factory canvas. Key symbols: DockCanvasInspectorAsync, SyncFloatingInspectorAsync.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82` — Shared shell used by Project Structure and Prompt Factory for eyebrow/title copy, stats, canvas slot, inspector slot, and supporting panels. Key symbols: CanvasWorkbenchStage.

## Related components

- CanvasWorkbenchStageShell
- HoverFocusRouter
- CanvasThemeTokenPack

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `FloatingInspectorHost` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
