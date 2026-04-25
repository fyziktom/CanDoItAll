# 04 Validation And Regression Proof

## Status

- Status: `Ready`

## Objective

- Prove the generic toolbox migration did not break existing canvases and that the new WebGL role-add toolbox flow works in a real browser.

## Covered Inputs

- R1: project and process structures canvases must keep working.
- R2: Playwright MCP screenshots must validate project structure block add and WebGL role add.

## Prerequisites

- Subbundle 01 completed with shared contract proof.
- Subbundle 02 completed with canvas host migrations.
- Subbundle 03 completed with WebGL toolbox authoring.

## Exact Source References

- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\ProjectStructureComposerDefaultsTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\PromptFactoryArtifactCaptureTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\OverlayWindowTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\WebGlWorkbenchInteropTests.cs

## Deliverables

- Build/test outputs recorded.
- Playwright MCP screenshots captured for project structure block add, WebGL role add, and migrated toolbox open states.
- Execution report updated with browser analytics, gate results, raw-note closure, and analytics review.

## Dependency Impact

- This subbundle closes the bundle only if all requested proof is real and recorded.
- If proof exposes weak earlier work, reopen the responsible subbundle before final closure.

## Validation Depth

- Run targeted builds for changed projects.
- Run targeted component/Playwright tests where reliable.
- Use Playwright MCP screenshots for required browser flows.
- Inspect screenshots for readability, clipping, layering, and visible post-add result.

## Implementation Steps

- Build OverlayLib, CanvasLib consumers, WebGlLib, WebGlSandbox, and/or the web app as needed.
- Start the web app or sandbox through dotnetwatch MCP when healthy; otherwise document fallback direct run.
- Validate project structure toolbox add flow with a real project ID.
- Validate WebGL role add flow on `/webgl/process-workbench`.
- Smoke process canvas and prompt factory toolboxes after migration.
- Update `reviews/01-execution-report.md`.

## Do Not Do

- Do not call the work complete without Playwright MCP screenshots.
- Do not treat static DOM existence as sufficient for post-add proof.
- Do not ignore project/process canvas regressions as unrelated.

## Acceptance Checklist

- All changed projects build or blockers are documented.
- Project structure block added from toolbox appears in canvas.
- WebGL role added from toolbox appears in 3D.
- Process canvas toolbox remains usable.
- Prompt factory toolbox remains usable.
- Execution report has complete browser analytics and raw-note closure.

## Proof Required

- Build/test command output summary.
- Screenshot paths under `C:\repositories\CanDoItAll\output\playwright-mcp\floating-component-toolbox`.
- Browser assertions for visible added project structure node and WebGL role node.
- Final bundle validator output.

## Browser Validation Logging

- Log route, viewport, Playwright MCP actions, assertions, screenshots, and result in the analytics table.

## Progression Gate

- Bundle final closure can pass only when browser analytics and raw note closure are complete and no required proof is missing.

## Suggested Agent Prompt

- Run the final validation matrix for the generic floating component toolbox. Use Playwright MCP to prove project structure block creation and WebGL role creation, inspect screenshots, update the execution report, and run the final bundle validator.
