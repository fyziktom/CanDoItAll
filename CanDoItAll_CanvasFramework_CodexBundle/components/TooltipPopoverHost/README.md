# TooltipPopoverHost

TooltipPopoverHost is a P1 shared high-level component in the category `Overlay, inspector, and helper components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Overlay, inspector, and helper components |
| Status | missing |
| Priority | P1 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 6 |

## Purpose

Provide unified tooltip and popover behavior for truncated text, validation messages, inline helpers, and contextual rich previews.

## Why this component is needed

Tooltip/popover support is explicitly missing in broader shared UI docs and is needed by upcoming canvas features.

## Main use cases

- Show full text for truncated labels.
- Display validation guidance or recommendation details near nodes.
- Provide small popovers for badges, anchor points, or minimap items.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

TooltipPopoverHost is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/TooltipPopoverHost.cs`
- `src/CanDoItAll.ComponentKit/Components/TooltipPopoverHost.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/tooltip-popover-host.js`
- `tests/CanDoItAll.Tests.Components/TooltipPopoverHostTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `docs/ui-shared-components/recommendations/missing-components.md#L1-L241` — Existing recommendation list that already calls out modal, tooltip, popover, and other shared UI gaps relevant to canvas work. Key symbols: Real tooltip / popover / context-menu system.
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866` — Main prompt builder/editor page. Uses the shared workbench shell but still builds graph projection, branch lanes, attachments, and UI-state persistence inline in the page. Key symbols: BuildCanvasNodes, BuildCanvasLinks, HandleCanvasSelectionChangedAsync, HandleCanvasNodesMovedAsync, HandleCanvasStateChangedAsync, PersistCanvasUiStateAsync, ....
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....

## Related components

- HoverFocusRouter
- CanvasThemeTokenPack
- TextBlockPrimitive

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `TooltipPopoverHost` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
