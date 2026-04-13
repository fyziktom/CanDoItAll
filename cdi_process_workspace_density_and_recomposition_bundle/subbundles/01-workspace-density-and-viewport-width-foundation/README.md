# Workspace density and viewport width foundation

## Status

- `Completed`

## Objective

- Make the processes workspace use its available width better on slight unzoom, add a badge-style `SummaryTile` mode, and apply the minimum density changes needed so the top of `/processes` spends less height.

## Covered Inputs

- `N001` Slight unzoom should use the available width.
- `N002` Add a badge-style `SummaryTile` prop.
- `N003` Save more height on the processes page.

## Prerequisites

- Bundle preparation validator must pass.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards\SummaryTile.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards\SummaryTiles.razor`
- `C:\repositories\CanDoItAll\Tailwind\surfaces\cards.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Graph\Interaction\ViewportController.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\SummaryTileTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- An opt-in badge-style `SummaryTile` presentation.
- `/processes` updated to use the badge-style tiles where that saves height.
- A width-usage fix so the definition canvas uses available horizontal space more honestly when zoom is slightly reduced.
- Focused automated coverage for the new tile mode and any workspace markup changes.

## Dependency Impact

- Later browser proof depends on this phase because the user explicitly asked for more usable height.
- Weak proof here would make the final screenshots hard to trust because the workspace could still waste space even if recomposition is correct.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Extend `SummaryTile` with an additive badge-style parameter and styling contract.
2. Apply that mode to the processes summary row and tighten any adjacent page spacing that still wastes height.
3. Identify the least invasive seam for the width-usage bug, likely in viewport-fit behavior or the process canvas host.
4. Add or update focused component tests.
5. Run browser proof on `/processes` at a large desktop viewport and a constrained-height follow-up.

## Scope Exceptions

- Smart process recomposition is explicitly out of scope here.

## Do Not Do

- Do not introduce a second metric component instead of extending `SummaryTile`.
- Do not add process-specific recomposition code in this phase.
- Do not mutate the managed SQLite workspace yet.

## Acceptance Checklist

- The summary row consumes less height and remains readable.
- Badge-style tiles keep label and value on one row.
- The canvas host no longer leaves obvious avoidable dead width after slight unzoom.
- Existing non-badge `SummaryTile` callers remain visually unchanged.

## Proof Required

- Focused component tests for `SummaryTile` and process workspace changes.
- Browser screenshots on `/processes` showing:
  - summary row height
  - badge-tile appearance
  - width occupancy after slight unzoom
- A short screenshot review note recorded in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route: `/processes`
- Viewports:
  - `1600x900`
  - one constrained-height desktop follow-up such as `1600x760`
- Required Playwright actions:
  - navigate to `/processes`
  - inspect the summary row
  - slightly reduce zoom or trigger the width-occupancy condition
  - capture screenshots
- Expected evidence paths:
  - `C:\repositories\CanDoItAll\output\playwright\process-workspace-density\01-summary-badge-row.png`
  - `C:\repositories\CanDoItAll\output\playwright\process-workspace-density\02-viewport-width-usage.png`
- Screenshot review questions:
  - Is dead width materially reduced?
  - Are badge-style tiles still legible?
  - Did any wrapping or clipping regress?

## Progression Gate

- `subbundles/02-shared-canvaslib-recomposition-engine-and-menu-contract` may start only after the badge-style summary tiles and width-usage fix both have browser proof and focused tests.

## Suggested Agent Prompt

```text
Implement this subbundle only. Extend SummaryTile with an additive badge-style mode, apply it to the process workspace to save height, fix the slight-unzoom width-usage problem with the smallest correct change, add focused tests, and prove the result on /processes at desktop and constrained-height viewports before closing the phase.
```
