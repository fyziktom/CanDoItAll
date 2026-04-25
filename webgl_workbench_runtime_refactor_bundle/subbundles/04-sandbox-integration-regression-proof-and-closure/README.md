# Sandbox integration, regression proof, and closure

## Status

- `Ready`

## Objective

- Finish the sandbox integration, remove or demote obsolete host-side stage controls, refresh the automated and manual browser proof, and close the bundle with honest raw-note coverage.

## Covered Inputs

- `N009` Playwright MCP and screenshot proof on the real WebGL result
- `RQ-15` through `RQ-18`

## Prerequisites

- `subbundles/01-runtime-foundation-refactor-and-api-shaping` completed and trusted
- `subbundles/02-in-scene-toolbar-and-settings-chrome` completed and trusted
- `subbundles/03-3d-connection-reconnection-and-delete-tools` completed and trusted

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\Components\Pages\ProcessWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\ProcessWebGlSandboxSession.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\wwwroot\webgl-sandbox.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\WebGlSandboxSmokeTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\WebGlSandboxPlaywrightFixture.cs`
- `C:\repositories\CanDoItAll\webgl_workbench_runtime_refactor_bundle\reviews\01-execution-report.md`

## Deliverables

- Sandbox host cleanup that no longer treats the old overlay/button/form controls as the primary stage-authoring surface.
- Updated Playwright automation and artifact expectations for the new stage-local chrome.
- Manual Playwright MCP proof with screenshots.
- Fully updated execution report and raw-note closure table.

## Dependency Impact

- This is the final closure phase.
- Weak proof here would leave the bundle incomplete even if the code changes are present.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Remove or demote obsolete host-side stage controls so the stage-local chrome is the primary authoring surface.
2. Update Playwright coverage to target the new toolbar, settings, menu, and authoring flows.
3. Run the automated proof and capture the required screenshots.
4. Run manual Playwright MCP validation on the live route and capture additional screenshots if needed.
5. Update bundle statuses, browser analytics, and raw-note closure rows.

## Scope Exceptions

- none planned; document any honest carry-forward item immediately if it appears.

## Do Not Do

- Do not hide missing browser proof inside a generic completion summary.
- Do not keep stale Playwright tests that still prove the old host-side control surface.
- Do not close the bundle while any raw note remains implicitly partial.

## Acceptance Checklist

- The sandbox route still renders and remains usable after the chrome migration.
- Playwright automation proves the new stage-local control surface rather than the retired HTML overlay/form.
- Manual Playwright MCP proof and screenshots are recorded.
- The execution report and raw-note closure rows are fully updated.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter WebGlSandboxSmokeTests`
- Manual Playwright MCP validation on `/webgl/process-workbench`
- Desktop screenshot review
- Narrow-width screenshot at `output/playwright/webgl-sandbox/bundle-04-narrow-width-proof.png`
- Completed raw-note closure table in `reviews/01-execution-report.md`

## Browser Validation Logging

- Routes: `/webgl/process-workbench` and `/webgl/process-workbench?template=branching-code-review`
- Viewport passes: `1900x1200` desktop and one narrower-width follow-up
- Required Playwright MCP actions:
- navigate to both routes
- prove the stage-local chrome is present
- open the relevant menu/settings states
- capture screenshots
- review the final scene and chrome layout
- Required review questions:
- Is the final route coherent on desktop?
- Does the chrome remain usable on the narrower pass?
- Are any obsolete host-side controls still carrying the main interaction burden?

## Progression Gate

- This bundle closes only when the automated tests, manual Playwright MCP proof, browser analytics, and raw-note closure table all agree that the requested WebGL refactor and authoring chrome work landed.

## Suggested Agent Prompt

```text
Implement this subbundle only.
```
