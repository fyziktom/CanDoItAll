# ProjectStructureValidationOverlay

ProjectStructureValidationOverlay is a P2 domain-specific high-level component in the category `Project Structure domain components`.

## Status summary

| Field | Value |
| --- | --- |
| Category | Project Structure domain components |
| Status | missing |
| Priority | P2 |
| Level | high-level |
| Scope | domain-specific |
| JS bridge | partial |
| Implementation wave | Wave 6 |

## Purpose

Render project-specific validation warnings, graph health indicators, and dependency issues directly on the canvas.

## Why this component is needed

The page already surfaces graph health information, but future richer authoring needs a first-class overlay instead of inspector-only messaging.

## Main use cases

- Show orphaned nodes, invalid dependencies, or required metadata warnings.
- Annotate nodes or connectors with warning badges or helper popovers.
- Support bulk validation review before publishing or syncing.

## Architectural context

Domain adapter layer inside `CanDoItAll.Modules.Workbench` bridging project-structure services and the shared graph framework.

## Current-state summary

ProjectStructureValidationOverlay is currently missing as a first-class component. The implementation should introduce it at the framework boundary defined in this bundle and wire it into the existing pages/services through the listed integration seams.

## Recommended target paths

- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureValidationOverlay.cs`
- `src/CanDoItAll.Modules.Workbench/wwwroot/js/project-structure-validation-overlay.js`
- `tests/CanDoItAll.Tests.Components/ProjectStructureValidationOverlayTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399` — Primary graph-editor page for project structure. Wires the shared workbench shell, builds nodes/actions, persists positions and view state, hosts inspector panels, and still contains several canvas-specific policies that should move into shared/domain adapter components. Key symbols: MapCanvasNode, HandleSelectionChangedAsync, HandleNodesMovedAsync, HandleCanvasStateChangedAsync, HandleCreateActionAsync, HandleCanvasActionAsync, ....
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806` — Domain models and service layer for project structure and project calendar, including persistence of links, node movement, UI state, and graph synchronization. Key symbols: ProjectStructureNode, ProjectStructureLink, ProjectStructureSurface, ProjectCalendarEvent, ProjectCalendarSurface, ProjectObjectCreateRequest, ....
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099` — Main shared graph runtime. Owns normalization, DOM/SVG rendering, measurement, links, group frames, context menus, create composers, drag, marquee, zoom, pan, maximize, and exported JS interop surface. Key symbols: mountFloatingInspector, normalizeSurface, buildNodeLookup, computeResolvedNodePositions, renderLinks, renderGroupFrames, ....

## Related components

- TooltipPopoverHost
- DiagnosticsOverlay
- ChipBadgePrimitive
- ConnectorPathPrimitive

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `ProjectStructureValidationOverlay` boundary in the recommended target path(s).
3. Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
4. Update `ProjectStructurePage.razor` so it consumes the adapter/policy rather than constructing graph behavior inline.
5. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
6. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
7. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
