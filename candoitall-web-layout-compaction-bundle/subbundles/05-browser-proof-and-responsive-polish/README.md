# browser-proof-and-responsive-polish

## Status

- `Completed`

## Objective

- Run the full browser closure pass, fix any remaining large-screen or responsive regressions exposed by the screenshots, synchronize the execution report, and close the bundle honestly.

## Covered Inputs

- Request note to execute the bundle after preparation
- Request note to analyze all pages and modals
- Request note that implementation quality should be checklisted and proven rather than guessed

## Prerequisites

- `subbundles/01-shell-foundations-and-layout-primitives`
- `subbundles/02-projects-page-and-project-modals`
- `subbundles/03-list-detail-pages-and-settings-density`
- `subbundles/04-workbench-and-prompt-factory-overlays`

## Exact Source References

- `C:\repositories\CanDoItAll\candoitall-web-layout-compaction-bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\candoitall-web-layout-compaction-bundle\plan\01-phase-plan.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- Completed browser analytics rows for every executed subbundle.
- Responsive follow-up fixes for issues found after the large-screen pass.
- Raw-note closure table updated from `Pending` to final status.
- Final bundle sync and validator pass.

## Dependency Impact

- This is the bundle closure phase.
- If this subbundle is weak, the initiative will still be at risk of shipping only partial or unreviewed layout fixes.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Re-run large-screen screenshots for every executed subbundle.
2. Re-check narrower widths wherever layout changed significantly.
3. Apply any residual spacing, wrapping, overflow, or readability fixes discovered in the browser.
4. Update the execution report tables and raw-note closure rows.
5. Run build and any targeted tests needed to confirm the final UI changes.
6. Run the prepared and completed bundle validators as closure gates.

## Scope Exceptions

- If the Playwright MCP browser remains blocked, keep using Playwright CLI and record that fallback honestly rather than pretending the proof path changed.

## Do Not Do

- Do not call the initiative complete because the code looks right without reviewed screenshots.
- Do not leave pending rows in the execution report after proof is complete.
- Do not treat missing overlay open-state proof as a minor residual risk.

## Acceptance Checklist

- Every executed subbundle has browser analytics and a gate result.
- Large-screen screenshots were reviewed, not just captured.
- Responsive regressions exposed by desktop fixes are addressed.
- Raw-note closure is explicit and honest.
- Final validator passes.

## Proof Required

- Completed `reviews/01-execution-report.md`
- Final build and targeted test commands
- Final prepared-stage revalidation if the bundle changed materially during execution
- Final completed-stage validator pass

## Browser Validation Logging

- Target routes: every affected route and modal family from subbundles 01-04
- Viewports: large desktop first, then narrower widths as needed
- Required browser actions:
  - rerun the proof flows
  - confirm no regressions after final fixes
- Required screenshot paths:
  - one reviewed final screenshot per major route family
  - one reviewed final screenshot per modal or overlay family
- Required review answers:
  - did the initiative actually solve the original complaint?
  - are other main pages materially more compact?
  - are modals and overlays efficient and unclipped?

## Progression Gate

- The bundle may close only after the execution report is fully populated, the raw notes are closed honestly, and the final validator passes.

## Suggested Agent Prompt

```text
Implement only subbundle 05.
Your job is not just proof capture; it is proof-driven cleanup, report synchronization, and final closure.
If browser evidence exposes a defect, fix it before you call the bundle complete.
```
