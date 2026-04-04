# Zyphonote cleanup and responsive Progress preservation

## Status

- `Completed`

## Objective

- Remove the temporary layout-comparison variants from Zyphonote `Progress` and keep only the responsive `Grid` + `Row` + `Column` implementation as the product-facing hero panel.

## Covered Inputs

- User request to move the comparison examples onto their own CanDoItAll sandbox page.
- User request to keep only the responsive row/column version in Zyphonote.
- User requirement to minimize custom structural styles and prefer shared layout components.

## Prerequisites

- Existing `Grid`, `Row`, and `Column` support already landed in `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout`.

## Exact Source References

- `C:\repositories\zyphonote\src\App.Blazor\Pages\Progress.razor`
- `C:\repositories\zyphonote\Tailwind\app-input.css`
- `C:\repositories\zyphonote\src\App.Web\Zyphonote.App.Web.csproj`

## Deliverables

- Zyphonote `Progress` page contains one production hero panel instead of four comparison variants.
- The retained hero panel uses the responsive `Grid` / `Row` / `Column` composition.
- The rest of the page remains visually intact.

## Dependency Impact

- The sandbox comparison page and MCP guidance depend on Zyphonote keeping only the canonical responsive version so the product page stops acting as an experiment surface.
- Weak proof here would make downstream guidance inconsistent with the shipped app.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Remove the temporary Stack, Grid-only, and fixed Row/Column comparison sections from `Progress.razor`.
2. Promote the responsive Row/Column version into the main hero position.
3. Remove any page-local helper markup or text that only existed for comparison.
4. Validate the route in the running watch app at desktop and narrow widths.

## Scope Exceptions

- Do not redesign the charts, stats, or data grid in this subbundle.
- Do not broaden this phase into other Zyphonote pages.

## Do Not Do

- Do not reintroduce custom structural wrappers when BaseLib layout primitives can express the shape.
- Do not move the responsive version out of Zyphonote.
- Do not change CanDoItAll theme styling in this phase.

## Acceptance Checklist

- Only one analytics hero remains on `/progress`.
- The remaining hero uses `Grid`, `Row`, and `Column`.
- Mode and time-window controls stay aligned on desktop and stack correctly on narrow widths.
- The page still builds and loads without regressions.

## Proof Required

- `dotnet build C:\repositories\zyphonote\src\App.Web\Zyphonote.App.Web.csproj -v:minimal`
- Browser screenshot for `/progress` at large desktop width.
- Narrow-width browser pass showing the second column wraps under the first.
- DOM or visual confirmation that only the responsive hero variant remains.

## Browser Validation Logging

- Target route: `http://localhost:5066/progress`
- Required viewports: maximized desktop, `900px`, and `390px`
- Required actions: load the page, verify one hero panel remains, verify controls remain readable, verify narrow-width stacking
- Required evidence paths: execution report entries plus screenshot artifact paths generated during validation
- Required review questions:
- Does the page still feel like a production screen rather than a sandbox comparison page?
- Does the responsive hero preserve the previously approved look?

## Progression Gate

- Zyphonote `/progress` must show only the responsive hero variant, with screenshot proof at desktop and narrow width, before the bundle can close.

## Suggested Agent Prompt

```text
Implement this subbundle only. Remove the temporary comparison sections from Zyphonote Progress, keep the responsive Grid/Row/Column hero as the only analytics panel, and prove the route at desktop and mobile widths.
```
