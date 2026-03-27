# 01 Refresh Progress And Marker Submenus

## Status

- `Completed`

## Objective

Make progress and marker submenu items readable at the requested larger size by moving progress text into the icon and increasing preset geometry until the hexes stop overlapping.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `R001`
- `R002`
- `R003`
- `R007`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\canvas-workbench.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- progress preset icons with center text for percentage and `N/A` values
- larger progress and marker preset metrics with non-overlapping placement
- browser assertions that the revised presets remain clickable and visually separated

## Implementation Steps

1. Update progress preset rendering so submenu icons can show center text without losing the ring state.
2. Increase progress and marker preset action metrics and related CSS sizing.
3. Retune label visibility so in-icon text does the work instead of competing with cramped external labels.
4. Extend browser coverage to measure overlap and confirm the resulting submenu state in screenshots.

## Scope Exceptions

- toolbar-safe nested-layer placement
- hover delay and loading-circle behavior
- hive-style submenu staggering

## Do Not Do

- do not replace progress rings with plain text badges
- do not enlarge priority presets into the same visual family unless required by the finished geometry
- do not close this subbundle without browser-level overlap proof

## Acceptance Checklist

- `10%`, `20%`, and `N/A` show inside the submenu icon center
- `Start` stays visually distinct without center text
- progress and marker preset hexes do not overlap in the validated browser state
- clicking the revised submenu items still updates node badges correctly

## Proof Required

- targeted browser assertions for submenu icon content and overlap absence
- screenshot artifact for the revised progress or marker submenu
- execution report updated with the exact browser test command and artifact path

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Keep the existing project-structure context menu system, but make progress and marker presets larger and readable. The progress value must move into the icon center, Start must stay intentionally blank, and the browser proof must show that the enlarged hexes no longer overlap.
```
