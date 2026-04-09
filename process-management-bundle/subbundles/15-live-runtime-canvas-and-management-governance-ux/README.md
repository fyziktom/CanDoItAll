# 15 Live Runtime Canvas And Management Governance UX

## Status

- `Completed`

## Objective

- Deliver the live runtime overlays, governance surfaces, and management-facing process UX while preserving projection-only rules and compact component-first layout.

## Covered Inputs

- `REQ-014`
- `REQ-015`
- `REQ-022`
- Legacy features `PRM-F20` and `PRM-F24`

## Prerequisites

- `13-project-activity-validation-and-process-projections`
- `14-agentframework-bridge-and-registry-convergence`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F20-change-governance-prioritization-literacy-and-management-adoption\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F24-live-process-execution-canvas-overlays-and-baton-visibility\README.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`
- `C:\repositories\CanDoItAll\process-management-bundle\shared-prompts`

## Deliverables

- Live process-run overlays on the authored canvas.
- Governance and management UX for change review, literacy, and operational supervision.
- Compact, component-first large-screen layout for dense management workflows.
- Clear visual separation between canonical definition state, runtime state, and overlay projection chrome.

## Dependency Impact

- Final analytics and conformance phases depend on these surfaces being clear, compact, and aligned with canonical ownership.
- If this subbundle is weak, management users will misread projections as truth and later analytics will be harder to trust.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add live runtime overlays to the authored process canvas.
2. Add governance and management-facing UX surfaces.
3. Keep the layout component-first and compact for large-screen use.
4. Prove overlays, governance views, and management pages in Playwright with screenshot review.

## Scope Exceptions

- Final deep analytics dashboards remain phase 04 work, but the navigation and operational surfaces must already be trustworthy here.

## Do Not Do

- Do not mutate canonical process state directly from overlay chrome.
- Do not rely on raw structural HTML when shared components can express the layout.
- Do not leave overlay clipping or layering defects to later phases.

## Acceptance Checklist

- Live overlays are clearly marked as projections.
- Governance and management views are readable and compact on large screens.
- Shared components are used intentionally.
- Browser proof confirms no clipping, collision, or layering regressions.

## Proof Required

- Playwright MCP walkthrough for overlay, governance, and management routes.
- Large-screen screenshots reviewed against readability, density, clipping, and layering questions.
- Component tests where appropriate for overlay behavior or state labels.

## Browser Validation Logging

- Route:
  live run canvas route
- Route:
  governance route
- Route:
  management or supervision route
- Viewport:
  `1920x1080`
- Viewport:
  `1600x900`
- Evidence:
  screenshots and Playwright actions recorded in the execution report

## Progression Gate

- Phase 04 may not start until the overlay and management UI surfaces are browser-validated, compact, and clearly projection-only.

## Suggested Agent Prompt

```text
Implement only the live runtime overlay and management UX slice. Keep overlays projection-only, use shared components first, optimize for large-screen density, and close only after Playwright and screenshot review prove the surfaces are stable.
```
