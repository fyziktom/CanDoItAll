# 06-06-browser-proof-and-closure

## Status

- `Completed`

## Objective

- Close the feedback bundle only after the implementation subbundles are revalidated, Playwright evidence and screenshots are captured, and every raw note has explicit proof recorded in the execution report.

## Covered Inputs

- `RQ-10`
- Final closure for `N001` through `N009`

## Prerequisites

- `01-01-visual-profile-and-palette-foundation` is completed.
- `02-02-catalog-expansion-and-type-mutation-flows` is completed.
- `03-03-inline-note-multiline-and-note-conversion` is completed.
- `04-04-node-id-copy-and-subtree-clipboard-workflows` is completed.
- `05-05-subtree-to-subproject-transfer` is completed.

## Exact Source References

- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureGraphAdapterTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageSimpleMutationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchSubtreeRecompositionIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs
- C:\repositories\CanDoItAll\project-structure-canvas-feedback-bundle-v1\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\project-structure-canvas-feedback-bundle-v1\plan\01-phase-plan.md

## Deliverables

- A rerun of the focused automated coverage affected by the implemented feedback set.
- A final Playwright MCP pass that exercises the shipped canvas behaviors and captures the required screenshots.
- A fully populated execution report with gate results, browser analytics, and raw note closure rows.
- A completed-stage bundle validator pass.

## Dependency Impact

- This phase is the final gate. Weak proof here invalidates the bundle regardless of how strong the individual code changes appear.
- Any reopened defect discovered here must flow back to the owning subbundle instead of being normalized as residual risk.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Re-run focused component, integration, and Playwright coverage for the full implemented surface.
2. Capture the named screenshots and review them against each subbundle’s questions.
3. Update `reviews/01-execution-report.md` so no row remains pending.
4. Run the completed-stage bundle validator and repair any final documentation or proof gaps.

## Do Not Do

- Do not mark the bundle complete while any execution-report row remains pending.
- Do not replace missing browser evidence with reasoning.
- Do not keep a subbundle in `Ready` or `In progress` status once final closure is claimed.

## Acceptance Checklist

- Focused automated coverage passes for the shipped feature set.
- Browser analytics rows are populated with actual route, viewport, evidence, screenshots, and results.
- Raw note closure rows all cite concrete proof.
- The completed-stage validator passes without bundle-structure or pending-proof failures.

## Proof Required

- Run the focused automated test suite selected for the implemented changes.
- Run the final Playwright MCP regression pass on `/projects/{projectId}/structure`.
- Capture or confirm the screenshots required by the earlier subbundles and any additional final-regression screenshots needed to explain closure.
- Run `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\project-structure-canvas-feedback-bundle-v1 --profile feedback --stage completed`.

## Browser Validation Logging

- Route under test: `/projects/{projectId}/structure`
- Required viewports: `1600x1000` large-screen regression and `1280x800` follow-up
- Required Playwright evidence: exercise the shipped color semantics, note editing, note conversion, copy actions, cut and paste, block mutation, and subtree-to-subproject transfer flows
- Required screenshots: reuse or refresh `01` through `05` screenshots as needed, plus `06-final-regression.png` if the final pass exposes combined state better
- Screenshot review questions: does the final integrated surface still look coherent after all changes, and does any combined-state regression require reopening an earlier subbundle

## Progression Gate

- The bundle may be marked complete only after all execution-report rows are populated, all raw notes show explicit proof, and the completed-stage validator passes.

## Suggested Agent Prompt

```text
Implement subbundle 06-06-browser-proof-and-closure only. Re-run the focused automated coverage, perform the final Playwright validation with screenshots, fully populate the execution report, and close the bundle only if the completed-stage validator passes.
```
