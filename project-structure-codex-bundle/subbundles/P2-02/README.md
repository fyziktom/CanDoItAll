# P2-02 Dedicated Screenshot And Performance Regression Suite

## Status
- Lifecycle status: `Ready`

## Objective
- Turn validated runtime states into a maintainable browser regression and performance suite.

## Covered Inputs
- Audit requirement for localized browser regression coverage and artifact capture.
- Feature preservation items `F01`, `F02`, `F03`, `F08`, `F12`, `F18`, `F21`, `F22`, `F23`, and `F33`.

## Prerequisites
- `P0-07` completed with trusted instrumentation proof.

## Exact Source References
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptLibraryVerificationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

## Deliverables
- More localizable Playwright coverage for ProjectStructure and shared canvas consumers.
- Deterministic screenshots for key workbench states.
- Performance-oriented browser assertions tied to available counters or fixtures.

## Dependency Impact
- Depends on instrumentation existing first.
- Final closure quality depends heavily on this suite being focused and repeatable.

## Validation Depth
- Playwright runs for the new or split regression coverage.
- Artifact review for screenshot stability.
- One targeted rerun proving that a precise subset can be executed independently.

## Implementation Steps
- Audit the current oversized smoke coverage.
- Split or reorganize tests only where that makes failures easier to localize.
- Keep artifact naming and storage deterministic.

## Do Not Do
- Do not duplicate unstable screenshots with no value.
- Do not widen back into feature implementation unrelated to browser coverage.

## Acceptance Checklist
- Browser regressions are easier to localize.
- Codex can rerun a precise subset of Playwright tests after each subbundle.

## Proof Required
- Targeted Playwright runs on the reorganized suite.
- Screenshot artifact paths captured in the report.
- Evidence that a precise subset can be rerun independently.

## Browser Validation Logging
- Route: ProjectStructure route plus `/prompt-factory` where shared coverage applies.
- Viewport: large-screen first.
- Record test names, artifact paths, and result in `reviews/01-execution-report.md`.

## Progression Gate
- Do not close the bundle without a regression suite that localizes failures to the relevant surface or behavior.

## Suggested Agent Prompt
- Refactor the browser regression coverage into more focused tests and artifact paths so future bundle tasks can rerun only the impacted surface with deterministic screenshots and counters.
