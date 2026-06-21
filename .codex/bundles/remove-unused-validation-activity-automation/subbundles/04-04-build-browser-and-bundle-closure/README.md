# 04-build-browser-and-bundle-closure

## Status

- `Completed`

## Objective

- Prove the app builds, relevant tests run, port `5032` is rebuilt and restarted, and Browser smoke validation passes after the module removals.

## Success Criteria

- Build command exits `0`.
- Relevant tests exit `0`, or any blocked test is recorded with a concrete reason.
- Fresh web host listens on port `5032`.
- Browser proof shows home and scheduler routes render and old module routes are not exposed in navigation.
- Completed bundle validator passes.

## Covered Inputs

- Raw note that the app must work again.
- Raw note to rebuild the running `5032` instance for testing.
- R007 and R008.

## Prerequisites

- SB03 direct-reference cleanup is complete.
- Product build is ready to run.

## Exact Source References

- `repo://src/CanDoItAll.Web`
- `repo://tests`
- `bundle://reviews/01-execution-report.md`

## Deliverables

- Build and test transcripts.
- Port `5032` restart transcript.
- Browser screenshots or DOM evidence.
- Completed bundle execution report and validator result.

## Dependency Impact

- This is the final closure gate for the whole request.

## Validation Depth

- End-to-end regression and closure: build, tests, host restart, Browser proof, and completed bundle validation.

## Implementation Steps

1. Run build and targeted tests.
2. Start the web app on port `5032`.
3. Use Browser against `http://localhost:5032/` and `/scheduler`.
4. Record navigation assertions and screenshots.
5. Update all bundle proof and run completed-stage validation.

## Scope Exceptions

- If a test suite is blocked by an environment dependency, record the command, exit code, and exact blocker rather than treating it as pass.

## Do Not Do

- Do not skip Browser proof for UI-visible removals.
- Do not leave a background dev server running on an unexpected port.

## Acceptance Checklist

- Build succeeds.
- Tests are executed or explicitly blocked with evidence.
- Port `5032` serves the restarted app.
- Browser proof verifies old module navigation is gone.
- Final bundle validator passes.

## Proof Required

- `bundle://proof/SB04/transcripts/build.txt`
- `bundle://proof/SB04/transcripts/tests.txt`
- `bundle://proof/SB04/transcripts/port-5032-restart.txt`
- Browser evidence paths recorded in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Route: `/`, `/scheduler`, and old route/nav absence checks.
- Viewport: desktop plus narrower-width follow-up if navigation wrapping is affected.
- Actions: navigate, wait for app render, inspect nav links/text, screenshot.
- Screenshot review: confirm no Validation, Activity, or Automation module entries are visible and SchedulerPlanner renders.

## Progression Gate

- Bundle may close only after build/test/Browser proof is recorded and completed-stage bundle validation passes.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Run build and relevant tests, restart the app on port 5032, verify home and scheduler routes with Browser, update proof manifests and the execution report, run completed bundle validation, and stop if any proof is missing.
```
