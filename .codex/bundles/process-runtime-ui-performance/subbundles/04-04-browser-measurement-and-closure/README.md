# 04 Browser Measurement And Closure

## Status

- Status: `Completed`

## Objective

Validate the optimized process page through tests and Playwright, record response timing, capture screenshot proof, and close every raw note.

## Covered Inputs

- N001: Process UI is slow when multiple process runs are active.
- N002: Visual Studio overhead does not excuse app-side slowness.
- N005: Core timing must be measured.
- N006: UI timing must be measured with Playwright MCP.
- N007: Do not break process functionality.

## Prerequisites

- `03-03-ui-observation-bottleneck-repair` closure gate passed.
- Targeted tests pass.
- Local app can start.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProcessManagementBundle.cs
- C:\repositories\CanDoItAll\README.md

## Deliverables

- Targeted test/build proof.
- Local server route for `/processes`.
- Playwright MCP timing for page load or interaction response.
- Screenshot proof path.
- Completed execution report with raw-note closure.

## Dependency Impact

- This is the final closure gate. If it fails, reopen the relevant earlier subbundle.

## Validation Depth

- Run targeted process tests and at least one build.
- Use Playwright against a warmed local app, not just an HTTP request.
- Capture route, viewport, action, assertion, screenshot path, and result.

## Implementation Steps

1. Run targeted tests and build.
2. Start the app locally.
3. Use Playwright MCP to navigate to `/processes` and measure response or interaction time.
4. Capture screenshot proof.
5. Fill execution report gate rows, analytics review, and raw-note closure.
6. Run completed-stage bundle validator.

## Do Not Do

- Do not treat a successful build as browser proof.
- Do not close the bundle with pending raw notes.

## Acceptance Checklist

- Core timing before and after is recorded.
- Browser timing and screenshot are recorded.
- Targeted tests pass.
- Raw notes are marked solved, partially solved, or not solved.

## Proof Required

- Test/build command output.
- Playwright timing output.
- Screenshot path.
- Completed-stage bundle validator output.

## Browser Validation Logging

- Route: `/processes?processId=9cfad5af-35c6-44d5-8938-50f889588534`
- Viewport: `1440x1000`
- Seed: temporary managed SQLite profile with one process definition and 16 active runs; original PostgreSQL workspace profile was restored afterward.
- Actions: navigate, wait for process heading, confirm interactivity, switch back to Definition, click Runs, wait for first and last active run headings.
- Assertions: 16-run history text visible; active run headings visible from run 16 through run 01; no blocking error surface.
- Timing: average heading visible `390 ms`, average interactive ready `1135 ms`, average Runs tab visible `128 ms`, max Runs tab visible `177 ms` over five samples.
- Screenshot: `.codex/bundles/process-runtime-ui-performance/measurements/processes-active-runs-optimized.png`.

## Progression Gate

- Passed. Browser proof, test proof, and build proof are recorded in `reviews/01-execution-report.md`.

## Closure Proof

- Playwright MCP returned five successful samples against the 16-active-run page.
- `dotnet build CanDoItAll.slnx -v:minimal` passed with `0` warnings and `0` errors.
- Active database selection after browser validation was restored to `PostgreSQL workspace` (`dc8abe54-58cd-4a87-98ab-5a14de6f846b`).

## Suggested Agent Prompt

Run final validation, start the app, measure `/processes` with Playwright, capture a screenshot, and update the bundle with command, timing, gate, and raw-note closure rows.
