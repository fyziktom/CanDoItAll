# Browser validation, regression repair, and closure audit

## Status

- `Blocked`

## Objective

- Finish the reopened route sweep, repair any browser-visible regressions, refresh the census if needed, close the raw prompt and follow-up feedback note by note, and answer the mandatory step `0` questions with facts.

## Covered Inputs

- `REQ-04`, `REQ-16`, `REQ-17`, `REQ-18`, `REQ-19`, `REQ-20`
- Raw prompt step `0`
- Raw prompt step `7`

## Prerequisites

- Subbundles `01` through `04` completed or honestly reopened.
- Browser analytics rows exist for every UI-affecting executed subbundle.

## Exact Source References

- `C:\repositories\CanDoItAll\solution-style-unification-bundle-v1\inputs\00-original-request.md`
- `C:\repositories\CanDoItAll\solution-style-unification-bundle-v1\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\solution-style-unification-bundle-v1\README.md`
- `C:\repositories\CanDoItAll\output\spreadsheet\style-census-initial.xlsx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- Final browser route sweep with regression repairs completed.
- Final metrics and refreshed census if the migration materially changed the raw utility landscape.
- A note-by-note closure result against the original prompt.
- A note-by-note closure result against the reopened follow-up feedback for `Home.razor`, `ProjectsPage.razor`, and `PromptFactoryPage.razor`.
- Honest answers to the four mandatory step `0` questions.
- Final bundle sync and validator pass.

## Dependency Impact

- This is the closure phase. If proof is weak here, the entire workflow remains incomplete.
- Any defect discovered here can reopen earlier critical foundations or migration work.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Re-run the route matrix and capture final screenshots on desktop and narrower widths.
2. Repair any remaining visual or behavioral regressions discovered during the sweep.
3. Refresh metrics and the census workbook if migration materially changed the remaining raw utility surface.
4. Close the original prompt and reopened follow-up feedback note by note in the execution report.
5. Answer the step `0` questions with facts and rerun the final validators.

## Scope Exceptions

- Any unresolved item must be marked `Partially solved` or `Not solved` with concrete proof and explicit follow-up. It must not be hidden in vague residual-risk prose.

## Do Not Do

- Do not declare success because the build passes if browser proof is still weak.
- Do not soften the final answers to the step `0` questions.
- Do not hide reopened work behind “residual risk” language.

## Acceptance Checklist

- Every executed UI-affecting subbundle has populated browser analytics rows and gate results.
- The original prompt is closed note by note with `Solved`, `Partially solved`, or `Not solved`.
- The final step `0` answers are explicitly justified by code changes and proof.
- Bundle documentation and code reality are synchronized.

## Proof Required

- Final `npm run build` from `C:\repositories\CanDoItAll\Tailwind`
- Final `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Any targeted test command chosen during execution
- Final Playwright screenshots for the migrated non-canvas route matrix
- Passing `validate_bundle.py --stage completed`

## Current Follow-Up Closure

- Browser proof now includes refreshed screenshots for `/`, `/projects`, and `/prompt-factory`.
- The reopened `Home.razor` and `ProjectsPage.razor` notes are closed with page-file census proof and browser screenshots.
- `PromptFactoryPage.razor` is still only partially closed. The route is revalidated and safer than before, but deeper non-canvas decomposition is still warranted.

## Browser Validation Logging

- Target routes: final non-canvas route matrix from subbundle `04` plus shell-level verification
- Required viewports: `1600x960`, `1280x900`, `1024x768`
- Required Playwright actions: route navigation, screenshot capture, and any focused interaction needed to prove repaired regressions
- Required screenshot findings: readable text, correct wrapping, no overlap, coherent spacing, and correct overlay layering on affected surfaces

## Progression Gate

- Final closure passes only when the bundle validator passes, the raw-note closure table is complete, and the step `0` answers are backed by real proof.

## Suggested Agent Prompt

```text
Run the final browser route sweep, repair any remaining regressions, refresh the metrics and census if needed, close the original prompt note by note, and answer the mandatory step-0 questions with facts before running the final bundle validator.
```
