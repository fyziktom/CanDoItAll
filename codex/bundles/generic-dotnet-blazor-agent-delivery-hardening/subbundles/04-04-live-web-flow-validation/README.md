# 04 Live Web Flow Validation

## Status

- Status: `In progress`

## Objective

Validate the updated agents and tools through the running CanDoItAll web app by creating two unrelated app-build process flows and observing agents build the apps under `C:\programovani\dotnet` without manual app-source repair.

## Covered Inputs

- User requirement to feed changes into the running web app.
- User requirement to test two small random-topic apps through project-structure and process-node flow.
- User requirement to observe agents and repair only generic process/skill/tool/agent guidance if they fail.

## Prerequisites

- Subbundle 03 has passed seed and build validation.
- Web app has been rebuilt and restarted with the updated seed catalog.
- `C:\programovani\dotnet` exists or is created as the external app output root.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureRuntimeLauncher.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.cs

## Deliverables

- Two process-created app outputs under `C:\programovani\dotnet`.
- Process run IDs, agent run evidence, build/test/run receipts, and browser proof.
- Execution report rows for browser validation and raw note closure.

## Dependency Impact

- This is final behavior proof for all earlier subbundles.
- If an app fails due to missing generic guidance or missing tool access, reopen subbundle 02 or 03.

## Validation Depth

- Real web app flow using browser/project-structure/process UI.
- Two unrelated random topics to prove genericity.
- Observe agent logs and outputs without manual app-source edits.
- Browser proof of the generated apps after agents complete.

## Implementation Steps

- Rebuild and start a fresh web app instance after source changes.
- Use the web UI to create simple project structures for two unrelated app topics.
- Add process nodes that start the exact app-build process for each topic.
- Start each process and observe agent execution to completion or concrete failure.
- Inspect generated directories, build/test/run receipts, browser screenshots, and process outcome.
- If failures are generic platform gaps, repair earlier subbundles and rerun.

## Scope Exceptions

- Provider/network outages may block live agent execution; record them as blockers only after verifying the app and process configuration are correct.

## Do Not Do

- Do not manually write or repair generated app source.
- Do not choose calculator, converter, unit-converter, or the prior validation topic.
- Do not claim success from process completion alone without inspecting generated app proof.

## Acceptance Checklist

- Two unrelated app topics are used.
- Both outputs are under `C:\programovani\dotnet`.
- Both processes use the updated generic agent/tool catalog.
- Build/test/run/browser evidence is captured or a generic platform failure is repaired and rerun.

## Proof Required

- Web app URL and process run identifiers.
- Generated app paths.
- Build/test/run receipts or equivalent process artifacts.
- Browser screenshots and observations for each generated app.

## Browser Validation Logging

- Log route, viewport, Playwright/browser actions, screenshot paths, and pass/fail result in `reviews/01-execution-report.md`.
- Capture one desktop-width proof and one narrower proof when the generated app is browser-facing.

## Progression Gate

- Bundle closure may proceed only when both random-topic process validations either pass without manual app-source repair or expose a documented generic blocker with a repair/rerun decision.

## Suggested Agent Prompt

Through the running web app, create two unrelated app-build process validations under `C:\programovani\dotnet`. Observe the agents without editing their generated app source. If they fail because platform guidance or tools are missing, repair the generic platform surface and rerun the validation.

