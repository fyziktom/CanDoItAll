# Validation And Closure

## Status

- `Completed`

## Objective

- Close the bundle with tests, browser screenshots, validator output, and raw-note closure.

## Success Criteria

- Targeted tests pass.
- Playwright MCP screenshots and assertions are recorded.
- Execution report contains command outcomes, browser analytics, subbundle gate results, and raw-note closure.
- Completed-stage bundle validator passes.

## Covered Inputs

- Final proof for N001, N002, N003 and R001 through R005.

## Prerequisites

- `01-tooltip-delay-coverage` closure gate passes.
- `02-module-navigation-contributions` closure gate passes.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\shell-menu-navigation-contributions`
- `C:\repositories\CanDoItAll\src`
- `C:\repositories\CanDoItAll\tests`

## Deliverables

- Execution report updated.
- Evidence screenshots saved.
- Final validator run recorded.

## Dependency Impact

- No downstream subbundle; weak proof blocks final bundle closure.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Run targeted tests.
2. Start or reuse the local web server.
3. Capture Playwright MCP assertions and screenshots.
4. Update execution report and root README validation summary.
5. Run completed-stage bundle validator.

## Scope Exceptions

- None expected.

## Do Not Do

- Do not add new product behavior beyond closing proof gaps discovered during validation.

## Acceptance Checklist

- All raw notes are marked `Solved`, or a concrete blocker/follow-up exists.
- No subbundle remains `Ready` or `In progress`.
- Completed-stage validator passes.

## Proof Required

- `dotnet test` targeted component/navigation tests.
- `python C:\Users\dell\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --stage completed codex\bundles\shell-menu-navigation-contributions`.
- Browser screenshots listed in the execution report.

## Browser Validation Logging

- Target route: `/agents`.
- Required viewport: desktop, `1440x900` or larger.
- Actions/assertions: combine tooltip timing and menu-order assertions.
- Screenshots: `evidence/menu-tooltip-delayed.png`, `evidence/agents-workflows-menu-order.png`.
- Review question: screenshots must support the same closure decision as tests and raw-note audit.

## Progression Gate

- Final closure only after tests, screenshots, raw-note audit, and completed-stage validator all pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
