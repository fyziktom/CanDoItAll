# browser-proof-and-closure

## Status

- `Completed`

## Objective

- Consolidate focused tests, real browser proof, screenshot review, execution-report updates, raw-note closure, and the completed-stage validator so the bundle can close with strong evidence.

## Covered Inputs

- `N001` through `N010` as final closure verification.

## Prerequisites

- `01-shortcut-contract-and-catalog-foundation` complete with proof.
- `02-runtime-keyboard-navigation-and-menu-affordances` complete with browser proof.
- `03-help-modal-information-architecture-and-shortcut-docs` complete with component and browser proof.

## Exact Source References

- `C:\repositories\CanDoItAll\project-structure-canvas-context-menu-shortcuts-bundle-v1\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasWorkbenchTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs`

## Deliverables

- Updated execution report with actual command outcomes, screenshots, browser analytics, and gate decisions.
- Final raw-note closure table marking each note as solved, partially solved, or not solved.
- Completed-stage validator output recorded in the bundle.
- Final residual-risk statement describing any remaining gaps or intentional deferrals.

## Dependency Impact

- This phase closes the entire bundle. Weak proof here would leave the final answer and future regression work without trustworthy evidence.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run the focused automated tests needed to support the changed surfaces.
2. Run or update Playwright proof on the project-structure canvas route.
3. Capture and review the required screenshots for menu behavior and help behavior.
4. Update the execution report with actual command results, browser analytics, gate decisions, and raw-note closure.
5. Run the completed-stage validator and repair any final bundle-structure issues before closure.

## Scope Exceptions

- Do not introduce new product scope in this phase.
- Only repair defects that directly block closure proof or validator success.

## Do Not Do

- Do not leave command outcomes or screenshot paths implied.
- Do not mark raw notes solved without explicit proof.
- Do not skip the completed-stage validator.

## Acceptance Checklist

- Required focused automated tests pass or are explicitly documented with a blocker.
- Browser proof covers keyboard menu flow and help-modal documentation flow.
- Execution report records real command outcomes and screenshot paths.
- Raw-note closure is explicit for `N001` through `N010`.
- Completed-stage validator passes.

## Proof Required

- Focused `dotnet test` command outputs recorded in the execution report
- Playwright evidence for keyboard-driven context-menu flows and help-modal browsing
- Screenshot files recorded in the execution report
- Completed-stage validator output recorded in the execution report

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewport pass: at minimum `1600x1000`, with narrower-width follow-up evidence already captured by earlier subbundles
- Playwright actions: verify menu keyboard path, verify help-page browsing path, capture final screenshots, note any visual issues
- Screenshot targets:
  - `evidence/context-menu-shortcuts-desktop.png`
  - `evidence/context-menu-shortcuts-narrow.png`
  - `evidence/help-modal-shortcuts-desktop.png`
  - `evidence/help-modal-shortcuts-narrow.png`
- Review questions:
  - Is the keyboard-first flow discoverable and reliable?
  - Do the final screenshots show the intended shortcut emphasis and help structure clearly?
  - Are there any visual or interaction regressions that require reopening a prior subbundle?

## Progression Gate

- Bundle closure is allowed only after all proof is recorded, raw notes are explicitly closed, and the completed-stage validator passes.

## Suggested Agent Prompt

```text
Implement only subbundle 04 for the canvas context-menu shortcuts bundle.
Run the focused automated tests, capture final browser proof and screenshots, update the execution report with actual outcomes, close each raw note explicitly, and finish with a passing completed-stage validator.
Only make code changes here if they are strictly necessary to repair a proof or validator blocker.
```
