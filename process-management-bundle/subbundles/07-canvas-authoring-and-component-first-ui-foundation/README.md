# 07 Canvas Authoring And Component-First UI Foundation

## Status

- `Ready`

## Objective

- Deliver the compact, component-first authoring UI foundation for process design, using CanvasLib and BaseLib rather than raw layout markup and leaving room for later runtime overlays.

## Covered Inputs

- `REQ-014`
- `REQ-015`
- Raw note `N09`
- Legacy feature `PRM-F09`

## Prerequisites

- `05-process-definition-lifecycle-and-governance-model`
- `06-role-templates-contracts-and-staffing-authoring`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F09-canvas-modeler-and-interactive-diagrams\README.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`
- `C:\repositories\CanDoItAll\process-management-bundle\templates`
- `C:\repositories\CanDoItAll\process-management-bundle\shared-prompts\qa-prompt.md`

## Deliverables

- Process authoring UI plan and implementation slice based on shared page, layout, navigation, form, and canvas primitives.
- Compact large-screen layout rules for dense process design surfaces.
- Authoring UX structure that already leaves room for later overlay and runtime chrome.
- Explicit component-first review rule before any custom structural CSS is accepted.

## Dependency Impact

- Runtime overlays, governance screens, and management views will reuse this UI foundation.
- If this subbundle falls back to raw layout wrappers, later UI phases will duplicate work and create inconsistent surfaces.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Use `candoitall-components-mcp` to choose the page scaffold, layout, navigation, and form primitives first.
2. Implement the process authoring surface on CanvasLib and shared page structure.
3. Keep the layout compact and optimized for large screens.
4. Prove the page in Playwright with screenshot review before closure.

## Scope Exceptions

- Live runtime overlays are deferred to phase 03.

## Do Not Do

- Do not build the authoring surface from raw `div` grids if shared components can express the layout.
- Do not optimize for mobile first at the cost of large-screen authoring clarity in this phase.
- Do not let canvas layout become the semantic source of truth.

## Acceptance Checklist

- Shared components are chosen and documented before custom layout CSS.
- Authoring UI uses available desktop width intentionally.
- Canvas editing remains readable, compact, and unclipped.
- The page leaves room for future overlay layers without redesigning the layout shell.

## Proof Required

- Playwright MCP route walkthrough.
- Large-screen screenshots reviewed for density, readability, clipping, and layering.
- Evidence that shared components were used or that any missing primitive was raised for BaseLib or CanvasLib improvement.

## Browser Validation Logging

- Route:
  `/processes`
- Route:
  process designer route
- Viewport:
  `1920x1080`
- Viewport:
  `1600x900`
- Follow-up:
  narrower-width pass only if layout materially changes
- Evidence:
  screenshot review questions must be answered in the execution report

## Progression Gate

- Phase 02 may not start unless the authoring UI has real browser proof, uses shared components intentionally, and shows no critical large-screen layout defects.

## Suggested Agent Prompt

```text
Implement only the authoring UI foundation for process design. Use shared BaseLib and CanvasLib primitives first, keep the surface compact for large screens, and close only after Playwright and screenshot review prove the layout.
```
