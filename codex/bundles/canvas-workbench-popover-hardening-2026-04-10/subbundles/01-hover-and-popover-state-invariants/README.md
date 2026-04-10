# Hover and popover state invariants

## Status

- `Completed`

## Closure Note

- Real workbench proof passed on `/projects/a17989b9-a8df-4671-9a3a-2d1fdcdfc2fd/structure`: annotation hover opened the popover, click cleared hover state, and re-hover did not reintroduce the original exception.

## Objective

- Establish a safe shared runtime contract for canvas annotation popovers by fixing the split-file `showPopover` access path and making canvas annotation hover state explicit, initialized, and resettable.

## Covered Inputs

- `N001` showPopover trouble in canvases
- `N003` explore the mechanism and make it more robust
- `R001` remove the uncaught exception
- `R003` initialize and clear canvas annotation hover state explicitly
- `R004` make popover rendering safe when popover chrome is unavailable

## Prerequisites

- `none`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\05-viewport-and-events.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js`

## Deliverables

- A safe popover entry path for `syncSceneHoverState`
- Explicit initialization for canvas annotation hover state
- Safe reset behavior for refresh and click paths that consume canvas annotation interaction
- Defensive popover show logic that does not throw on missing or disconnected popover chrome

## Dependency Impact

- Subbundle 02 depends on this phase because every broader canvas-interaction hardening change assumes the base hover and popover path is valid.
- Subbundle 03 cannot trust browser proof if this foundation still allows uncaught exceptions or stale annotation keys.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Replace the unsafe direct canvas-renderer popover call with a valid split-file access pattern.
2. Add the missing canvas annotation hover-state initialization and reset points.
3. Harden popover show logic so missing or disconnected popover DOM pieces do not throw.
4. Validate that the fix preserves existing annotation action behavior before downstream hardening starts.

## Scope Exceptions

- `none`

## Do Not Do

- Do not change the C# `CanvasWorkbenchSurface` contract.
- Do not rewrite unrelated overlay systems.
- Do not introduce silent fallback behavior that hides real runtime state bugs.

## Acceptance Checklist

- No uncaught exception occurs when hovering a canvas annotation hot zone.
- Canvas annotation hover state is initialized in the base workbench state.
- Refresh and canvas-annotation click paths do not leave stale annotation hover keys behind.
- DOM badge annotations still keep their existing popover behavior.

## Proof Required

- Targeted build or test confirmation on the relevant CanvasLib consumer project after the JS changes land.
- Browser proof on `/groups/canvas` showing that annotation hover opens the popover without console errors.
- A large-screen screenshot with the popover visible.
- A narrower-width follow-up pass if layout or overlay placement changes.

## Browser Validation Logging

- Route under test: `/groups/canvas`
- Required viewport passes: `1600x900` first, then `1280x800` if the popover position changed
- Required Playwright actions: navigate, hover a visible annotation, inspect console, click the related node or annotation action, and re-hover to confirm the popover can still open
- Expected screenshots: one large-screen open-popover screenshot for this phase
- Required visual review: readable content, no clipping, no harmful lateral overflow, correct layering above neighboring chrome

## Progression Gate

- Downstream work may continue only after the shared canvas route proves that annotation hover no longer throws and the same annotation can still present a popover after a click or refresh-triggering interaction.
- Closure decision: satisfied on the real workbench route. The sandbox route was later inspected and found to have no annotation-bearing sample nodes, so it could not act as the primary proof surface for this bug.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Fix the shared CanvasWorkbench popover entry path and the base canvas annotation hover-state invariants without widening scope into unrelated canvas systems.
```
