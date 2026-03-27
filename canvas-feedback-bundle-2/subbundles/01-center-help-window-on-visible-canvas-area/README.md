# 01 Center Help Window On Visible Canvas Area

## Objective

Center the shared help overlay within the visible canvas stage instead of pinning it near the top.

## Covered Inputs

- `N001`
- `R001`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasWorkbenchTests.cs`

## Deliverables

- centered help overlay behavior in shared canvas CSS
- no regression to the existing help markup or toggle behavior

## Implementation Steps

1. Keep the overlay rendered from `CanvasWorkbench.razor`.
2. Update the shared help overlay CSS so it centers inside the stage surface.
3. Keep the help card height-constrained and scrollable when viewport height is limited.

## Do Not Do

- do not move the help overlay into page-specific markup
- do not break the settings overlay that reuses the same shared overlay styling

## Acceptance Checklist

- opening help places the dialog in the stage center rather than at the top edge
- the help card remains fully readable on a constrained viewport

## Proof Required

- focused component test pass
- bundle execution report updated with the validation command

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Center the shared canvas help overlay inside the visible stage area without changing how the help toggle works. Keep the fix in the shared canvas layer and preserve mobile-safe scrolling behavior.
```
