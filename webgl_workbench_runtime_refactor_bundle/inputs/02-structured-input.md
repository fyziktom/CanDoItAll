# Structured Input

## Core Objective

- Replace the current monolithic WebGlLib runtime with a maintainable split implementation and close the most visible authoring gaps so the sandbox offers real in-scene authoring chrome instead of relying on external Razor controls for the core workbench actions.

## Hard Constraints

- Keep the work inside the existing WebGlLib and WebGlSandbox concept scope; do not silently expand into the production `ProcessWorkspace`.
- Preserve or consciously update the public automation bridge exposed as `window.CanDoItAll.webglWorkbench`.
- The top toolbar and right-click menu requested by the user must be drawn in the WebGL runtime surface, not left as ordinary host HTML controls.
- Provide settings for node info density with three explicit states: miniature, detailed, and hidden.
- Identify and implement at least one additional useful scene setting beyond the requested node-info density modes.
- Validate the actual rendered result with Playwright MCP actions and screenshots on the WebGL sandbox route.

## Source Artifacts

- Raw request preserved in `inputs/00-original-request.md`
- Current runtime and surface contract files listed in `inputs/01-source-artifacts.md`
- Existing concept bundle at `C:\repositories\CanDoItAll\webgl_process_workbench_concept_bundle`
- Existing automated proof surfaces in `tests/CanDoItAll.Tests.Playwright\WebGlSandboxSmokeTests.cs`

## Input Coverage Signals

- `N001` Exact request to split `01-webgl-workbench.js` into logical smaller classes and helpers.
- `N002` Exact request to analyze how CanvasLib is done without blindly copying its still-large files.
- `N003` Exact request for a right-click menu that is also drawn in WebGL.
- `N004` Exact request for tools for connection and reconnection of nodes in 3D.
- `N005` Exact request for a top toolbar similar to canvas, but really drawn in WebGL.
- `N006` Exact request for tools for delete, selection, and related authoring actions.
- `N007` Exact request for settings to show miniature node info, detailed node info, or no node info.
- `N008` Exact request to identify another useful option or setting.
- `N009` Exact request to test the real WebGL result with Playwright MCP and screenshots.

## Dependency And Sequencing Signals

- Runtime decomposition is the first critical foundation because the toolbar, context menu, and 3D authoring tools all need stable internal boundaries.
- The WebGL toolbar and settings chrome are a second critical UI foundation because the connect, reconnect, delete, and selection tools need a stable in-scene tool-mode surface.
- Updated browser proof and Playwright coverage must wait until the runtime split and in-scene chrome stop shifting.

## Validation Expectations

- Build and targeted .NET test runs must pass for the WebGlLib/WebGlSandbox affected areas.
- Existing or updated Playwright automation must prove render, toolbar/settings behavior, and 3D authoring flows.
- Manual Playwright MCP proof must confirm the real toolbar, menu, settings, and authoring actions on the live route with screenshots.
- The bundle execution report must capture browser analytics and raw-note closure row by row.

## UI Validation Strategy

- First pass on `/webgl/process-workbench?template=branching-code-review` at a large-screen viewport around `1900x1200`.
- Capture and review screenshots for readability, overlap, clipping, hierarchy, space usage, and whether the stage-local chrome actually feels integrated instead of bolted on.
- Open the context menu state and the settings state explicitly; do not only prove the closed state.
- After the desktop pass is stable, rerun at a narrower width to confirm the stage and WebGL chrome still fit and remain usable.

## Browser Validation Analytics

- Each UI-relevant subbundle will log route, viewport, Playwright MCP actions, core assertions, screenshot paths, and pass/fail outcome in `reviews/01-execution-report.md`.
- Desktop screenshots will land under `output/playwright/webgl-sandbox/` and be cited in the execution report.
- Manual MCP checks will be recorded alongside automated test commands so the proof stays tied to the route under test.

## Working Assumptions

- The page hero and non-stage explanatory cards may remain host-rendered; the requested toolbar and context menu must move into the WebGL surface.
- Deletion may stay sandbox-local and resettable as long as it behaves like a real authoring tool inside the concept host and is honestly documented.
- The current WebGL automation hooks (`getSceneSnapshot`, `getState`, `simulateDrag`, `simulateConnection`, `exportImageData`) should remain available or receive a backwards-compatible replacement because both tests and manual proof depend on them.

## Primary Risks

- The runtime split could accidentally break the current automation bridge or interop entry points.
- A WebGL-drawn HUD can easily become unreadable or hard to hit-test if the scene and chrome layers are not separated cleanly.
- Edge selection and reconnection are riskier than node selection because the current runtime only raycasts node meshes.
- Existing Playwright smoke tests currently target host HTML controls and will fail until the proof surface is updated.
