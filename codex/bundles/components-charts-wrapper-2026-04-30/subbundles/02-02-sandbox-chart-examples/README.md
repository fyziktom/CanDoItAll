# 02-sandbox-chart-examples

## Status

- `Completed`

## Objective

Add a new components sandbox page/group that proves the chart wrapper with common Apex-inspired cases.

## Covered Inputs

- N002, N007, N008, N009
- Requirements: R007, R008, R009

## Prerequisites

- `01-01-wrapper-foundation` completed with build proof and wrapper API accepted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\DataDisplay.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Layout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\App.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\_Imports.razor`
- `C:\repositories\CanDoItAll\codex\bundles\components-charts-wrapper-2026-04-30\inputs\screenshots\Screenshot 2026-04-25 085501.png`
- `C:\repositories\CanDoItAll\codex\bundles\components-charts-wrapper-2026-04-30\inputs\screenshots\Screenshot 2026-04-25 085436.png`
- `C:\repositories\CanDoItAll\codex\bundles\components-charts-wrapper-2026-04-30\inputs\screenshots\Screenshot 2026-04-25 085323.png`

## Deliverables

- New route `/groups/charts`.
- New Charts group in sandbox navigation and examples registry.
- Examples for pie/donut share, single-line datetime trend, multi-line trend, filled area, color tuning, labels, units, legend, and toolbar.
- Generated sample data held in the sandbox page or a small sandbox-only helper.
- Desktop and mobile screenshot evidence.

## Dependency Impact

- Final closure depends on this phase to prove the wrapper actually works in the browser.
- If the page compiles but charts render blank or examples miss required cases, raw-note closure cannot pass.

## Validation Depth

- Critical UI proof: build plus real browser DOM assertions and screenshots.

## Implementation Steps

1. Add chart services/assets to the sandbox host.
2. Add `SandboxGroupKey.Charts`, chart group metadata, and chart examples.
3. Create `Components/Pages/Charts.razor` using `CatalogPageFrame`, `Grid`, `SectionCard`, and `CdaChart`.
4. Generate realistic sample data for required chart cases.
5. Build the sandbox.
6. Start the sandbox and validate `/groups/charts` in a browser.
7. Capture desktop and mobile screenshots and review them.

## Scope Exceptions

- No product module will use the charts yet because the user explicitly said no suitable real cases exist now.
- The sandbox samples are illustrative generated data, not EnergoApp data imports.

## Do Not Do

- Do not add a marketing landing page.
- Do not use raw Apex components directly in the sandbox page.
- Do not introduce one-off page-wide structural CSS when shared layout components can express the page.
- Do not over-copy EnergoApp visual branding; use CanDoItAll sandbox visual system.

## Acceptance Checklist

- `/groups/charts` appears in sandbox navigation.
- The page includes pie/donut, line, multi-line, area fill, color tuning, labels, units, legend, and toolbar examples.
- Desktop screenshot shows charts nonblank and readable.
- Mobile screenshot shows no harmful clipping/overlap.
- Browser DOM checks confirm Apex-generated chart content exists.

## Proof Required

- `dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj` -> passed with 0 warnings, 0 errors.
- Playwright navigate to `/groups/charts` -> passed on `http://127.0.0.1:55174/groups/charts`.
- DOM assertion for chart containers and Apex SVG elements -> passed via `evidence/check-charts.js`.
- Screenshot artifacts under `codex/bundles/components-charts-wrapper-2026-04-30/evidence/` -> `charts-desktop.png` and `charts-mobile.png`.

## Browser Validation Logging

- Route: `/groups/charts`.
- Viewports: large desktop first, then mobile.
- Actions/assertions: navigate, wait for chart DOM, count `.apexcharts-svg` or equivalent rendered elements, screenshot, review.
- Expected artifacts: `evidence/charts-desktop.png`, `evidence/charts-mobile.png`.
- Review questions: readability, overlap, clipping, hierarchy, shared system fit, required case coverage.

## Progression Gate

- Passed. Browser proof confirms the chart examples render nonblank, desktop and mobile screenshots are readable, and no reopened fix is required.

## Suggested Agent Prompt

```text
Implement the sandbox charts page only, using the chart wrapper created in phase 01. Add the catalog group and examples, then prove `/groups/charts` with desktop and mobile browser screenshots.
```
