# RecommendationOverlay

RecommendationOverlay is a P2 domain-specific high-level component in the category `Prompt Factory domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Prompt Factory domain components |
| Status | missing |
| Priority | P2 |
| Level | high-level |
| Scope | domain-specific |
| JS bridge | partial |
| Implementation wave | Wave 6 |

## Purpose

Display AI-driven or rules-driven recommendations directly on the Prompt Factory canvas as contextual badges, callouts, or action popovers.

## Why this component is needed

PromptFactoryService already exposes recommendation-related behavior, and future roadmap items will likely require in-canvas recommendation UX.

## Main use cases

- Show suggested next blocks or missing inputs near a selected prompt node.
- Present accept/reject actions for recommendations directly on the canvas.
- Drive future onboarding or best-practice guidance in the editor.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Factory` bridging prompt-session state and the shared graph framework.

## Current-state summary

RecommendationOverlay is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.Modules.Factory/CanvasAdapters/RecommendationOverlay.cs`
- `src/CanDoItAll.Modules.Factory/wwwroot/js/recommendation-overlay.js`
- `tests/CanDoItAll.Tests.Components/RecommendationOverlayTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715` — Prompt Factory domain service handling recommendations, state persistence, branching, build/export/send flows, and canvas UI-state persistence. Key symbols: GetRecommendedBlockIdsAsync, SaveSessionStateAsync, SaveCanvasUiStateAsync, BranchNodeAsync, UpdateNodeAsync, SetNodeStateAsync, ....
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645` — Prompt Factory domain create/action catalog used to populate canvas context actions and create menus. Key symbols: BuildSessionContextActions, BuildSelectionContextActions, BuildComponentNodeActions, BuildInputNodeActions.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....

## Related components

- TooltipPopoverHost
- ChipBadgePrimitive
- HoverFocusRouter
- SelectionModel

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `RecommendationOverlay` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Update `PromptFactoryPage` partial files so graph projection/history/toolbox behavior is consumed through the adapter boundary.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
