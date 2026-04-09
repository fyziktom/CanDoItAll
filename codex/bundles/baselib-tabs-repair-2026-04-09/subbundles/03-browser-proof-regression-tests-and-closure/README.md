# Browser Proof Regression Tests And Closure

## Status

- `Completed`

## Objective

- Close the work with targeted regression tests, headed browser proof on the dedicated tabs route, screenshot review, raw-note closure, and final bundle synchronization.

## Covered Inputs

- `N007` revalidate after example-driven discoveries
- `N009` execute the prepared bundle to completion
- `N010` validate the finished tabs work with Playwright and screenshots

## Prerequisites

- `subbundles/01-shared-tabs-foundation-and-cad-style-unification` is `Completed`
- `subbundles/02-sandbox-tabs-lab-and-edge-case-coverage` is `Completed`
- The dedicated tabs sandbox route already passed its closure gate

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\TabsComponentTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\NavigationTabs.razor`
- `C:\repositories\CanDoItAll\codex\bundles\baselib-tabs-repair-2026-04-09\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\codex\bundles\baselib-tabs-repair-2026-04-09\traceability\01-requirement-traceability.md`

## Deliverables

- Focused regression tests covering the repaired tabs contract
- Headed browser proof with saved screenshots for the dedicated tabs route
- Updated execution report, raw-note closure table, and final bundle status
- Final prepared and completed validator passes

## Dependency Impact

- This is the bundle closure phase. If the proof is weak here, the entire repair remains untrusted and should not be marked complete.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Add or finalize focused component-test coverage for the repaired tabs component.
2. Start or reuse the managed sandbox app session and confirm the dedicated route loads the repaired component.
3. Use headed Playwright CLI automation to validate desktop and narrower-width views on the dedicated route.
4. Save screenshots, answer the visual-review questions, and record analytics while the route is open.
5. Reopen earlier phases immediately if the final proof reveals a foundation or sandbox-surface defect.
6. Complete the raw-note closure table, update bundle statuses, and run the final validator.

## Scope Exceptions

- None. This subbundle exists specifically to remove proof gaps rather than defer them.

## Do Not Do

- Do not claim completion from build or tests alone.
- Do not write `tested manually` in place of headed browser evidence.
- Do not leave raw notes in `Not started` or `Pending` states at closure.
- Do not hide missing screenshots or missing browser assertions inside residual-risk prose.

## Acceptance Checklist

- Focused component tests pass for the repaired tabs contract.
- The dedicated tabs route is validated in a headed browser with saved screenshots.
- Desktop and narrower-width visual review answers are documented.
- The raw-note closure table is fully updated.
- No executed subbundle remains `Ready` or `In progress`.
- The final prepared and completed validator stages pass.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter Tabs`
- Any necessary focused build command if test output alone is insufficient
- Headed Playwright CLI session against the dedicated tabs route
- Screenshot artifacts under `output/playwright/baselib-tabs-repair-2026-04-09/`
- `python C:\Users\dell\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\codex\bundles\baselib-tabs-repair-2026-04-09 --profile feedback --stage completed`

## Browser Validation Logging

- Target route: `/groups/navigation/tabs`
- Viewports:
- `1600x900`
- `900x1024`
- `390x844`
- Required Playwright actions:
- open the route in a headed browser
- click through at least two tab sets or states
- inspect example headings and fallback text
- capture screenshots at each required viewport
- Screenshot artifact names:
- `output/playwright/baselib-tabs-repair-2026-04-09/03-closure-desktop.png`
- `output/playwright/baselib-tabs-repair-2026-04-09/03-closure-tablet.png`
- `output/playwright/baselib-tabs-repair-2026-04-09/03-closure-mobile.png`
- Required screenshot review answers:
- all text readable
- active, hover, disabled, and bordered states look intentional
- no overlap, clipping, or awkward gaps
- route still feels coherent with the app’s shared visual system

## Progression Gate

- This is the final phase. The bundle may close only when the tests pass, the browser artifacts and analytics are recorded, the raw notes are all solved or explicitly blocked, and the final bundle validator passes.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Finish the repaired tabs work with focused component tests, a headed Playwright CLI validation pass on the dedicated tabs sandbox route across desktop and narrower widths, screenshot review, raw-note closure updates, and final bundle-validator passes. Reopen earlier phases immediately if the browser proof reveals a remaining tabs defect.
```
