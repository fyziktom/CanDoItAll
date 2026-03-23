# ContextMenuHost

ContextMenuHost is a P0 shared high-level component in the category `Overlay, inspector, and helper components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Overlay, inspector, and helper components |
| Status | partial |
| Priority | P0 |
| Level | high-level |
| Scope | shared |
| JS bridge | required |
| Implementation wave | Wave 2 |

## Purpose

Own context-menu placement, nested menus, keyboard dismissal, focus handling, and action dispatch.

## Why this component is needed

A robust context menu exists in the workbench JS, but it is still embedded and mixed with create flow behaviors. It should be elevated into a reusable overlay component.

## Main use cases

- Project Structure node actions, create menus, and utility commands.
- Prompt Factory node and session context actions.
- Future clipboard, validation, and diagnostics menu groups.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

ContextMenuHost already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/ContextMenuHost.cs`
- `src/CanDoItAll.ComponentKit/Components/ContextMenuHost.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/context-menu-host.js`
- `tests/CanDoItAll.Tests.Components/ContextMenuHostTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645` — Prompt Factory domain create/action catalog used to populate canvas context actions and create menus. Key symbols: BuildSessionContextActions, BuildSelectionContextActions, BuildComponentNodeActions, BuildInputNodeActions.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326` — Domain action catalog and label resolver for Project Structure create flows and inspector create groups. Key symbols: ResolveNodeLabel, BuildMenuCreateActions, BuildInspectorCreateGroups, TryResolveCreateDefinition.
- `docs/ui-shared-components/recommendations/missing-components.md#L1-L241` — Existing recommendation list that already calls out modal, tooltip, popover, and other shared UI gaps relevant to canvas work. Key symbols: Real tooltip / popover / context-menu system.

## Related components

- HoverFocusRouter
- KeyboardShortcutRouter
- TextBlockPrimitive
- IconGlyphPrimitive

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `ContextMenuHost` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
