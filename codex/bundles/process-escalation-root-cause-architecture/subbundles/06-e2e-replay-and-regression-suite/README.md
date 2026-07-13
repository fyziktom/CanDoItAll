# E2E Replay And Regression Suite

## Status

- `Ready`

## Objective

- Prove the process escalation fixes end to end with deterministic replay scenarios for simple .NET delivery, UI/browser proof, missing capability diagnostics, and management-only suppression.

## Success Criteria

- A simple .NET development process run advances past the previously blocked setup/implementation stage without unnecessary manager escalation.
- UI/browser proof steps can access required browser/Playwright/screenshot capability when declared.
- Missing tool/MCP/skill scenarios block early with typed readiness diagnostics.
- Management-only steps do not receive suppressed development context.

## Covered Inputs

- R09 Regression Suite.
- R01 through R08 as final closure.
- User need to test E2E process runs on the 5032 instance.

## Prerequisites

- SB01 completed.
- SB02 completed.
- SB03 completed.
- SB04 completed.
- SB05 completed.
- 5032 instance can be rebuilt and restarted from the final source.

## Exact Source References

- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `repo://Templates/Agents/teams/dotnet-delivery`
- `repo://Templates/Agents/teams/visual-automation-templates`

## Deliverables

- Regression tests for the observed escalation categories.
- End-to-end replay instructions and proof for a simple .NET delivery process.
- Browser-proof replay when UI/browser capability is declared.
- Negative replay for missing MCP/tool readiness.
- Negative replay for management-only development suppression.
- Final closure report with commands, run ids, screenshots when relevant, and process API readback excerpts.

## Dependency Impact

- This is the closure phase. Any failure here reopens the earlier subbundle matching the failed root cause.
- E2E failure without typed diagnostics reopens SB01.
- E2E missing capability or wrong suppression reopens SB02.
- E2E fallback/retry misclassification reopens SB03.
- E2E domain leak or .NET policy issue reopens SB04/SB05.

## Validation Depth

- End-to-end regression and closure.

## Implementation Steps

1. Add deterministic fixtures for simple .NET delivery, UI/browser proof, missing capability, and management-only suppression.
2. Run targeted unit and integration suites from earlier subbundles.
3. Build the web app and restart the 5032 instance.
4. Clear old Calculator/Tetris process-run output artifacts only when they are from previous test runs and not source data.
5. Launch the E2E process replay and record run ids.
6. Monitor process API/projections for status, diagnostics, readiness, and artifact lineage.
7. Capture browser/screenshots only for scenarios that declare browser proof.
8. Write final execution report with pass/fail and reopen decisions.

## Scope Exceptions

- Do not use this phase to introduce new architecture. Reopen the responsible earlier subbundle instead.
- Do not accept a manual "it worked" result without process API/readback proof.

## Do Not Do

- Do not clear user-owned project output unless the path is explicitly identified as old process-run artifact output.
- Do not force Playwright into non-UI or management-only scenarios.
- Do not ignore a blocked run if diagnostics are missing.
- Do not treat Calculator/Tetris success as proof that arbitrary app topics work.

## Acceptance Checklist

- Simple .NET delivery run reaches its expected validation/release point without the previous escalation.
- UI/browser proof step records browser capability and screenshot evidence when declared.
- Missing capability scenario fails early with typed readiness diagnostics.
- Management-only suppression scenario confirms dev tools/skills are not injected.
- Final API readback includes diagnostic/recovery/artifact lineage fields.
- 5032 is rebuilt/restarted for user testing after final implementation.

## Proof Required

- `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --no-restore --configuration Debug`
- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter Process`
- `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter Process`
- Playwright proof only for declared UI/browser scenario.
- Process API readback for each replay run.
- Screenshot paths for UI/browser proof scenario only.

## Browser Validation Logging

- Target route or window: local 5032 process workspace and any generated app route only when the process step declares browser proof.
- Required viewport passes: desktop plus one narrower viewport for generated UI proof.
- Playwright MCP actions or assertions: navigate, snapshot/evaluate visible state, capture screenshot, check console status.
- Screenshot file names or evidence paths: record in `reviews/01-execution-report.md` during implementation.
- Screenshot review questions: is the app nonblank, route correct, visible behavior aligned with process request, and evidence tied to the process run id.

## Progression Gate

- Bundle may close only when E2E replay proves the fixes or reopens the responsible earlier subbundle with typed diagnostics.

## Suggested Agent Prompt

```text
Implement this subbundle only after SB01-SB05 are complete.
Run the final regression suite and E2E replays. Rebuild and restart the 5032 instance, launch the scoped process scenarios, capture process API proof and browser proof only where declared, update the execution report, and reopen the responsible earlier subbundle if any run blocks without typed diagnostics.
```
