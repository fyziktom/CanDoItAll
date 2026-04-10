# Canvas runtime hardening across node interactions

## Status

- `Completed`

## Closure Note

- The hardening sweep covered both the canvas and legacy DOM annotation paths, validated anchor rectangles before reuse, centralized stale hover cleanup through `hidePopover`, and raised the popover layer above the toolbar after browser proof exposed visual occlusion.

## Objective

- Harden the shared canvas annotation popover mechanism across the relevant node and interaction paths so clicks, rerenders, and related hover transitions do not reintroduce stale state or nearby JS anti-patterns.

## Covered Inputs

- `N002` happens in workbench canvas, usually when clicking some node
- `N004` analyze it for all nodes and situations
- `N005` check similar JS anti-patterns and preserve functionality
- `R002` cover shared annotation-bearing node render paths
- `R005` preserve current functionality
- `R006` repair nearby JS anti-patterns in the same mechanism

## Prerequisites

- `01-hover-and-popover-state-invariants` completed with trusted proof

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03-interaction-and-state.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\05-viewport-and-events.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Canvas.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`

## Deliverables

- Hardened canvas annotation click and hover-reset behavior
- Re-anchor or reopen behavior that stays coherent when the same annotation remains logically hovered after rerender
- Audit-driven cleanup for nearby hover and popover anti-patterns in the same runtime path
- Preserved annotation behavior for shared canvas consumers

## Dependency Impact

- Subbundle 03 depends on this phase because closure proof must cover more than the original crash. If this sweep is weak, browser proof can still miss stale-state regressions that appear after clicks or rerenders.

## Validation Depth

- `UI, regression, and browser-proof`

## Implementation Steps

1. Audit the canvas annotation registration and click paths that touch popover state.
2. Patch stale-state or same-key hover issues that can surface after click, refresh, or rerender.
3. Review nearby overlay and hover helpers for the same split-file or null-guard anti-patterns and make the smallest safe fixes.
4. Re-run focused validation before handing off to closure.

## Scope Exceptions

- `none`

## Do Not Do

- Do not refactor unrelated workbench modules or C# pages.
- Do not change non-popover interaction semantics unless the existing behavior is objectively unsafe or inconsistent with the bug report.
- Do not add consumer-specific branches for one route when the shared runtime can be fixed once.

## Acceptance Checklist

- Shared canvas annotation hover still works across the node render paths that register annotation hot zones.
- Clicking annotation-related targets does not leave stale hover state that blocks later popovers.
- Delete mode, dependency mode, and overlay-target guards still behave as before.
- No new console errors are introduced in the shared canvas runtime.

## Proof Required

- Targeted validation on the relevant project or test scope after the hardening changes land.
- Browser proof on `/groups/canvas` that repeats hover and click interactions across multiple annotation-bearing nodes.
- Workbench-route smoke proof on `/projects/{ProjectId}/structure` if a seeded project is available.
- Screenshots of the open popover state on at least one shared-canvas route.

## Browser Validation Logging

- Primary route: `/groups/canvas`
- Secondary route: `/projects/{ProjectId}/structure` when environment data allows it
- Required viewport passes: `1600x900` and `1280x800`
- Required Playwright actions: hover multiple annotations, click the related node or annotation action, trigger a rerender or state change if available, and confirm the popover can reopen without console errors
- Expected screenshots: one large-screen screenshot and one follow-up screenshot if workbench-route proof is available
- Required visual review: readable popover text, no clipping, no overlap behind floating windows or neighboring chrome

## Progression Gate

- Closure work may continue only after browser proof shows that the shared canvas route remains stable across repeated hover and click sequences and no stale annotation-hover state remains.
- Closure decision: satisfied on the workbench route. Sandbox inspection remains documented as a sample-data limitation rather than a runtime blocker.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Harden the shared CanvasWorkbench canvas-annotation interaction path across clicks, rerenders, and nearby hover-state helpers while preserving the current workbench feature set.
```
