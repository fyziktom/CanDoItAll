# CanvasThemeTokenPack

CanvasThemeTokenPack is a P1 shared low-level component in the category `Utility and infrastructure components`.

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

Centralize canvas/workbench theme tokens for color, spacing, radii, shadows, line weights, typography, and dark/light readiness.

## Why this component is needed

The current look is mostly encoded in a large CSS file and specialized widget styles. A formal token pack is needed for long-term consistency and skinning.

## Main use cases

- Apply consistent card, connector, overlay, and backdrop styling across Project Structure, Prompt Factory, and Calendar.
- Enable a future dark mode without patching dozens of runtime-specific style fragments.
- Expose stable theming hooks to DALL-E prompt generation and visual QA.

## Architectural context

Shared canvas framework inside `CanDoItAll.ComponentKit`, consumed by Project Structure and Prompt Factory through explicit adapters.

## Current-state summary

CanvasThemeTokenPack already has meaningful logic in the repository, but that logic is fragmented, embedded, or missing a clean boundary. The implementation should extract and harden the existing behavior instead of rewriting from scratch.

## Recommended target paths

- `src/CanDoItAll.ComponentKit/Canvas/Core/CanvasThemeTokenPack.cs`
- `tests/CanDoItAll.Tests.Components/CanvasThemeTokenPackTests.cs`

## Relevant existing repository files

- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309` — Visual design system for the workbench host, node cards, inspector, context menus, overlays, and supporting panels. Key symbols: cw-* CSS rules.
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720` — Large specialized calendar widget that already contains its own view renderers, panel, list, editor, export flows, and hit handling. This is a sibling runtime, not a graph scene graph. Key symbols: CalendarController.prototype.updateOptions, destroy, renderPanel, renderList, persistEvent, render, ....
- `docs/ui-shared-components/recommendations/missing-components.md#L1-L241` — Existing recommendation list that already calls out modal, tooltip, popover, and other shared UI gaps relevant to canvas work. Key symbols: Real tooltip / popover / context-menu system.

## Related components

- CanvasSceneHost

## Recommended implementation approach

1. Inspect the referenced files and mark the exact logic that currently owns this responsibility.
2. Create the new `CanvasThemeTokenPack` boundary in the recommended target path(s).
3. Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
4. Wire the new component into the nearest shared or domain adapter seam instead of layering it directly into page code.
5. Add or update tests at the most appropriate level (pure logic, component wrapper, or page regression).
6. Remove or deprecate the old ownership point once parity is reached.

## Reuse / refactor rule

Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
