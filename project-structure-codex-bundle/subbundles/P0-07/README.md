# P0-07 Instrumentation And Browser Gates Foundation

## Status
- Lifecycle status: `Ready`

## Objective
- Add the measurement and screenshot foundation needed to prove later refactors.

## Covered Inputs
- Audit requirement for counter-based performance evidence and repeatable browser gates.
- Feature preservation items `F02`, `F21`, `F33`, and `F34`.

## Prerequisites
- None.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptLibraryVerificationTests.cs`

## Deliverables
- Debug counters or equivalent runtime measurements for render and state-publication behavior.
- Repeatable screenshot scenarios for critical workbench states.
- Browser tests that fail explicitly when required visual or interaction gates regress.

## Dependency Impact
- Critical foundation for every later performance claim and browser-proof requirement.
- Direct prerequisite for `P1-01` and `P2-02`.

## Validation Depth
- Browser proof that counters are observable.
- Playwright artifact capture for key ProjectStructure and PromptFactory states.
- Smoke validation that failing browser gates are surfaced as test failures.

## Implementation Steps
- Audit current diagnostics and test artifact capture.
- Add only the counters and assertions needed to support later subbundles.
- Keep the public JS surface stable while exposing measurement data.

## Do Not Do
- Do not add instrumentation that changes runtime behavior materially.
- Do not call this complete with screenshots alone and no assertions.

## Acceptance Checklist
- Codex can prove improvements with counters and screenshots rather than anecdotes.
- A failing browser gate is treated as a failed task.

## Proof Required
- Targeted Playwright runs for ProjectStructure and PromptFactory where shared files change.
- Screenshots stored in the repository’s normal artifact path.
- Counter or diagnostics output accessible during browser validation.

## Browser Validation Logging
- Route: ProjectStructure structure route and `/prompt-factory`.
- Viewport: large-screen first.
- Record counters, artifact paths, assertions, and result rows in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start `P1-01` or `P2-02` until the measurement and browser-gate foundation is proven usable.

## Suggested Agent Prompt
- Add the smallest useful render and state counters plus deterministic screenshot coverage so later performance claims and regressions can be proven instead of guessed.
