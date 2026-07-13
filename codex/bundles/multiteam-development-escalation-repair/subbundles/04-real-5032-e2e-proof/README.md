# real-5032-e2e-proof

## Status

- `Completed`

## Objective

- Rebuild, restart the 5032 development instance, reload updated templates, and prove the simple Calculator multiteam run no longer repeats the false escalation loop.

## Success Criteria

- Solution build passes.
- 5032 starts against `candoitall_development`.
- Template reload/preflight reflects updated contracts.
- Prior broken run data is cancelled or isolated before the fresh run.
- Fresh process run reaches the corrected implementation/validation route without repeated false escalation.

## Covered Inputs

- R6, R7.

## Prerequisites

- SB02 targeted tests pass.
- SB03 targeted tests pass.

## Exact Source References

- `C:\repositories\CanDoItAll`
- `C:\repositories\CanDoItAll\src\App\CanDoItAll.Web\appsettings.Development.json`

## Deliverables

- Build output.
- Runtime restart and template reload proof for `http://localhost:5032`.
- Fresh real process run id and status evidence.
- UI/browser proof if the process reaches app validation.

## Dependency Impact

- This is the final closure proof. Weak evidence here reopens SB02 or SB03.

## Validation Depth

- End-to-end process regression and closure.

## Implementation Steps

1. Run targeted tests and full build.
2. Restart 5032 with development database.
3. Confirm templates are loaded with new contracts.
4. Cancel/isolate stale failed runs where needed.
5. Launch a fresh Calculator multiteam development run.
6. Monitor run state until the prior false escalation path is proven fixed or a concrete unrelated blocker appears.
7. Capture browser/UI proof if the run reaches visual validation.

## Scope Exceptions

- External provider outage or quota exhaustion can be recorded as an external blocker only after template/readiness routing proof is captured.

## Do Not Do

- Do not mark closure from static tests alone.
- Do not delete unrelated process history.

## Acceptance Checklist

- 5032 is running on port 5032.
- Development DB is active.
- Updated process contracts are visible in runtime launch/preflight output.
- Fresh run evidence shows no repeated feature-child escalation loop.

## Proof Required

- Build/test commands and outputs.
- Runtime URL and health output.
- Process run id and step status table.
- Browser screenshot path and review notes if UI proof is reached.

## Browser Validation Logging

- Route: record fresh runtime URL returned by the process validation step, not a guessed localhost URL.
- Viewports: large desktop first; narrower follow-up if UI is reached.
- Actions/assertions: record Playwright/browser actions and visible Calculator assertions.
- Screenshot paths: record any screenshots in `reviews/01-execution-report.md`.
- Review findings: answer whether the UI proposal image was considered by QA if visual-target assets are present.

## Progression Gate

- Final closure requires no repeat of the prior escalation loop, or a precise unrelated blocker with routing proof.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
