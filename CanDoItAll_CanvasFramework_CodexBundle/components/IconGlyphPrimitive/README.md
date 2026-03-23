# IconGlyphPrimitive

IconGlyphPrimitive is a P1 shared low-level component in the category `Basic primitives`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Basic primitives |
| Status | partial |
| Priority | P1 |
| Level | low-level |
| Scope | shared |
| JS bridge | none |
| Implementation wave | Wave 2 |

## Purpose

Render small icon glyphs, state icons, and symbolic markers with unified sizing and alignment rules.

## Why this component is needed

Icons are currently embedded ad hoc in cards, chips, and menu entries instead of being a shared primitive.

## Main use cases

- Node type icons in Project Structure and Prompt Factory.
- Toolbar and context menu icons in the workbench shell.
- Small semantic markers such as warning, progress, attachment, or locked state.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

IconGlyphPrimitive already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Graph/IconGlyphPrimitive.cs`
- `tests/CanDoItAll.Tests.Components/IconGlyphPrimitiveTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645` — Prompt Factory domain create/action catalog used to populate canvas context actions and create menus. Key symbols: BuildSessionContextActions, BuildSelectionContextActions, BuildComponentNodeActions, BuildInputNodeActions.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326` — Domain action catalog and label resolver for Project Structure create flows and inspector create groups. Key symbols: ResolveNodeLabel, BuildMenuCreateActions, BuildInspectorCreateGroups, TryResolveCreateDefinition.

## Related components

- CanvasThemeTokenPack
- TextBlockPrimitive

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `IconGlyphPrimitive` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
