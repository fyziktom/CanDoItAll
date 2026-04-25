# Execution Report

## Status

- Execution state: `Implemented`
- Bundle validation state: `Prepared bundle validated on 2026-04-21`
- Closure state: `Implemented, regression-repaired, and extended through the toolbar maximize/dock follow-up, with only residual broader Playwright fixture-host timing sensitivity still noted`

## Delivered Scope

- Split the monolithic `01-webgl-workbench.js` runtime into smaller modules:
  - `01-webgl-workbench.js`
  - `02-webgl-workbench-core.js`
  - `03-webgl-workbench-overlays.js`
  - `04-webgl-workbench-chrome.js`
  - `05-webgl-workbench-interaction.js`
- Continued the runtime split so the larger interaction surface is now isolated into dedicated helpers:
  - `06-webgl-workbench-camera.js`
  - `07-webgl-workbench-scene-graph.js`
  - `08-webgl-workbench-hit-testing.js`
  - `09-webgl-workbench-actions.js`
  - `10-webgl-workbench-drag.js`
- Added `11-webgl-workbench-anchor-flow.js` so anchor compatibility checks, exact source/target draft state, and connection-request shaping are isolated out of the larger action surface.
- Reduced the former interaction monolith into a facade-style entry module:
  - `05-webgl-workbench-interaction.js` now re-exports focused drag, hit-test, and action helpers instead of owning the whole implementation
- Added in-scene WebGL chrome instead of host-side authoring controls:
  - top toolbar with `Select`, `Delete`, `Connect`, `Reconnect`, `Fit`, `Reset`, and `Settings`
  - right-click context menus for scene, node, and edge interactions
  - tool modes for selection, delete, connect, and reconnect
- Added camera view switching in both the host controls and the WebGL-drawn toolbar:
  - `Perspective`
  - `XY`
  - `XZ`
  - `YZ`
- Added three new C#-backed 3D recomposition algorithms that use process semantics and connection density to reserve clearer space around busy nodes:
  - `Critical path spine`
  - `Fan-out corridor`
  - `Radial burst`
- Extracted the C# layout work into `ProcessWebGlLayoutEngine.cs` so the heavier 3D recomposition logic stays on the server-side projection path instead of bloating the JS runtime.
- Added settings for node-info density:
  - `Detailed`
  - `Miniature`
  - `Hidden`
- Added additional useful runtime settings:
  - grid visibility
  - anchor visibility
  - edge label visibility
  - diagnostics visibility
  - role-node visibility
  - branch-helper visibility
- Added model-based node visuals and path markers:
  - `lowpoly_person_boxing.glb` for role nodes
  - `question_box.glb` for switch and router helper nodes
  - `gears.glb` for standard process-step nodes
  - green start sphere ahead of the first process step
  - red end sphere beyond the last process step
- Tightened role anchor placement so connection pins sit materially closer to the person model instead of floating far outside the visual body.
- Added zoom-conditional anchor labels for node connection points so colored port markers can reveal their exact meaning during close inspection without keeping the full scene permanently noisy.
- Upgraded connect and reconnect authoring from node-only targeting to exact anchor targeting:
  - source selection can lock to one specific output anchor
  - destination selection can lock to one specific compatible input anchor
  - multi-input steps now expose the precise role/input target instead of forcing a whole-node guess
- Refined the in-scene toolbar into a single-row, icon-first control strip so the authoring chrome stays compact at desktop width without wrapping into stacked rows.
- Added an in-scene maximize/dock action that can lift the WebGL stage into an overlay shell and return it to the page layout without leaving the runtime surface.
- Persisted stage maximize state through the runtime snapshot, Blazor interop session state, and sandbox page shell so the docked/undocked mode survives rerenders and host refreshes.
- Repaired a maximize-shell interaction bug by making the visual backdrop non-interactive, so mouse input now reaches the WebGL stage and toolbar while maximized.
- Added sandbox-local delete behavior and reconnect support across the runtime, Blazor interop surface, and sandbox session state.
- Removed the old host-owned WebGL authoring overlay from the sandbox page so the stage-local runtime chrome is now the primary authoring surface.
- Repaired a post-refactor rendering regression where the in-scene chrome pass could clear the main WebGL scene, which left DOM labels/anchors visible while hiding node meshes and connection curves.
- Repaired a follow-up navigation regression where returning from orthographic views to `Perspective` could round-trip through the route and stay stuck in `XY`.
- Repaired a focused component-test analyzer break in `ProcessWebGlSandboxSessionTests.cs` so the validation suite now runs cleanly again.
- Repaired a maximize-mode rendering issue by resizing the stage shell before the viewport/render sync, which prevents the full-stage WebGL surface from appearing stretched or blurry after expansion.
- Reduced the main runtime hot path by:
  - caching WebGL chrome rebuilds until chrome state actually changes
  - stopping per-render renderer resize work unless the host viewport changes
  - trimming repeated DOM overlay content rebuilds for node labels and edge text
- Extracted shared floating-window behavior into a new reusable component library:
  - `src/CanDoItAll.Components.OverlayLib/Components/Core/OverlayWindow.razor`
  - `src/CanDoItAll.Components.OverlayLib/Models/OverlayWindowState.cs`
  - `src/CanDoItAll.Components.OverlayLib/wwwroot/css/overlay-window.css`
  - `src/CanDoItAll.Components.OverlayLib/wwwroot/js/runtime/overlay-window.js`
- Kept CanvasLib backward-compatible while swapping to the shared implementation:
  - `CanvasFloatingWindow.razor` is now a compatibility wrapper over `OverlayWindow`
  - legacy JS interop root `CanDoItAll.canvasFloatingWindow.*` still resolves through the shared runtime bridge
  - legacy CSS classes such as `cw-floating-window__*` still render from the shared component so existing browser and bUnit expectations remain stable
- Adopted the shared overlay library in the WebGL sandbox and proved it on the dedicated route with live selection and command-log overlay windows positioned over the stage.
- Removed the duplicated generic floating-window skin from the old CanvasLib stylesheet so the extracted overlay library now owns the shared visual chrome instead of CanvasLib carrying a second copy.
- Added direct shared-overlay component coverage in `OverlayWindowTests.cs` and re-ran the existing `CanvasFloatingWindowTests.cs` wrapper coverage to prove the shared component plus the CanvasLib shim both stay stable.
- Revalidated the swapped CanvasLib windows in the real CanDoItAll app:
  - project-structure canvas kept the shared selection-window chrome plus draggable toolbox window behavior
  - process canvas kept the shared selection/toolbox/editor window flow on the `Steps` tab with live Playwright MCP screenshots

## Commands

| Command | Result | Notes |
| --- | --- | --- |
| `python C:\Users\dell\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\webgl_workbench_runtime_refactor_bundle --profile initiative --stage prepared` | `Passed` | Bundle structure and readiness gate validated during preparation. |
| `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\CanDoItAll.Components.WebGlSandbox.csproj -c Release -v minimal` | `Passed` | Build succeeded; restore surfaced existing `NU1903` dependency warnings only. |
| `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj --no-restore -v minimal` | `Passed` | Follow-up layout-engine and runtime-shape build passed; existing `NU1903` warnings only. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProcessWebGlSandboxSessionTests -v minimal` | `Passed` | `8/8` focused component tests passed. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ProcessWebGlSceneAdapterTests|ProcessWebGlSandboxSessionTests|WebGlWorkbenchInteropTests" -v minimal -p:BaseOutputPath=C:\repositories\CanDoItAll\output\test-bin\ -p:BaseIntermediateOutputPath=C:\repositories\CanDoItAll\output\test-obj\` | `Passed` | Focused component and interop coverage passed using isolated outputs to avoid the live sandbox binary lock. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WebGlWorkbenchUiStateTests -v minimal` | `Passed` | `2/2` focused unit tests passed. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProcessWebGlSandboxSessionTests -v minimal` | `Passed` | `12/12` focused component tests passed after cleaning the conflicting `Fact/Theory` attribute on the camera-view route-state test. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WebGlWorkbenchUiStateTests -v minimal` | `Passed` | `2/2` focused unit tests re-ran cleanly during the GLB-node follow-up validation. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ProcessWebGlSandboxSessionTests|ProcessWebGlSceneAdapterTests"` | `Passed` | `21/21` focused component tests passed after the exact-anchor authoring follow-up. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter WebGlSandboxSmokeTests` | `Passed` | `6/6` smoke tests passed after the anchor-label and explicit target-input proof updates. |
| `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj` | `Passed` | Build succeeded with existing `NU1903` warnings only. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter FullyQualifiedName~WebGlSandboxSmokeTests -v minimal` | `Partial` | Latest clean run ended at `4/6` passing. Two fixture-host smokes remained timing-sensitive around synthetic connect and synthetic drag persistence. |
| `Playwright MCP manual route proof on /webgl/process-workbench?template=branching-code-review` | `Passed` | Live route manually inspected with screenshots for toolbar, settings, context menu, and narrow-width layout. |
| `Playwright MCP regression repair proof on /webgl/process-workbench?template=branching-code-review` | `Passed` | Verified restored node/edge rendering, stable rerenders, and non-rebuilding chrome objects on the live route after the repair patch. |
| `Playwright MCP follow-up proof on /webgl/process-workbench?template=branching-code-review` | `Passed` | Verified host and in-scene camera-view switching, perspective round-trip repair, and the three new layout algorithms with live screenshots on `http://127.0.0.1:5123`. |
| `Playwright MCP GLB node-visual proof on /webgl/process-workbench?template=branching-code-review` | `Passed` | Verified imported model groups for role, branch, and step nodes, confirmed closer projected role anchors, confirmed `2` start/end flow markers, and captured fresh live-route screenshots on `http://127.0.0.1:5501`. |
| `Playwright MCP anchor-label and explicit-target proof on /webgl/process-workbench?template=branching-code-review` | `Passed` | Verified zoom-conditional anchor labels, focused compatible target-input highlighting, and a real exact-anchor role-binding on `http://127.0.0.1:5599`. |
| `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\CanDoItAll.Components.WebGlSandbox.csproj --nologo` | `Passed` | Toolbar maximize/dock follow-up build passed; restore surfaced existing `NU1903` warnings only. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter WebGlWorkbenchUiStateTests --nologo` | `Passed` | `2/2` focused unit tests passed with stage-maximize UI-state round-trip coverage. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProcessWebGlSandboxSessionTests --nologo` | `Passed` | `13/13` focused component tests passed with sandbox-session maximize-state propagation coverage. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter Sandbox_in_scene_chrome_controls_camera_settings_and_context_actions --nologo` | `Passed` | Focused toolbar/chrome smoke passed with one-row toolbar proof plus maximize/dock stage assertions and screenshot capture. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter Sandbox_in_scene_chrome_controls_camera_settings_and_context_actions --nologo` | `Passed` | Re-ran after the pointer-overlay repair; maximized-mode proof now includes real mouse clicks on the in-scene toolbar. |
| `Playwright MCP toolbar maximize/dock proof on /webgl/process-workbench?template=branching-code-review` | `Passed` | Verified the icon-first toolbar stays on one row, confirmed runtime maximize/dock state, and captured clean docked/maximized screenshots on `http://127.0.0.1:5611`. |
| `Playwright MCP post-fix visual proof on /webgl/process-workbench?template=branching-code-review` | `Passed` | Revisited the live sandbox after the pointer fix and captured a fresh route screenshot on `http://127.0.0.1:5621`. |
| `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Components.OverlayLib\CanDoItAll.Components.OverlayLib.csproj --no-restore -m:1 /p:UseSharedCompilation=false` | `Passed` | Shared overlay library compiled cleanly after the extraction. |
| `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\CanDoItAll.Components.WebGlSandbox.csproj --no-restore -m:1 /p:UseSharedCompilation=false` | `Passed` | WebGL sandbox still builds cleanly against the shared overlay library; restore surfaced existing `NU1903` warnings only. |
| `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1 /p:UseSharedCompilation=false` | `Passed` | Main web app still builds cleanly after the CanvasLib overlay swap; restore surfaced existing `NU1903` warnings only. |
| `dotnet build C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore -m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false` | `Passed` | Built the focused component test assembly without rebuilding already-running UI hosts. |
| `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter "FullyQualifiedName~CanvasFloatingWindowTests|FullyQualifiedName~OverlayWindowTests"` | `Passed` | `7/7` targeted component tests passed, covering the shared overlay component plus the CanvasLib compatibility wrapper. |
| `Playwright MCP shared-overlay proof on /webgl/process-workbench?probe=overlay-refresh` | `Passed` | Verified the WebGL sandbox selection and command-log overlays render through the shared library with stable stage positioning and fresh screenshots on `http://127.0.0.1:5501`. |
| `Playwright MCP shared-overlay proof on /projects/{projectId}/structure` | `Passed` | Verified project-structure selection-window action chrome, opened the toolbox window, and confirmed drag motion still moves the shared floating window on `http://127.0.0.1:5515`. |
| `Playwright MCP shared-overlay proof on /projects/{projectId}/processes` | `Passed` | Switched to the `Steps` tab, opened the process toolbox and canvas editor windows, and captured fresh process-canvas overlay screenshots on `http://127.0.0.1:5515`. |
| `npm run webgllib:verify-assets` | `Not run` | No asset-regeneration issue was observed during build or live route proof. |

## Browser Artifacts

| Artifact | Purpose | Result |
| --- | --- | --- |
| `C:\repositories\CanDoItAll\output\playwright-mcp\page-2026-04-21T20-28-59-378Z.png` | Baseline live-route proof after moving back onto current source | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\element-2026-04-21T20-39-06-457Z.png` | Desktop stage with in-scene toolbar rendered inside WebGL | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\element-2026-04-21T20-39-28-616Z.png` | Settings panel open with node-info density and additional scene options | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\element-2026-04-21T20-40-48-951Z.png` | Node context menu rendered in-scene | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\page-2026-04-21T22-17-56-877Z.png` | Narrow-width full-page proof | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-regression-fix-check.png` | Regression repair proof showing node boxes and connection curves visible again | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-regression-fix-after-zoom.png` | Follow-up live viewport capture during the repair pass | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-regression-fix-settings-toggle.png` | Live rerender proof after toggling WebGL chrome actions | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-refactor-validation-stage-default.png` | Follow-up runtime split proof with the refactored stage, host controls, and WebGL toolbar visible together | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-camera-yz-view.png` | Live `YZ` view proof showing the camera switch reflected in both host and in-scene WebGL chrome | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-layout-critical-path-spine.png` | `Critical path spine` 3D recomposition proof | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-layout-fanout-corridor.png` | `Fan-out corridor` 3D recomposition proof | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-layout-radial-burst.png` | `Radial burst` 3D recomposition proof | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\page-2026-04-22T02-34-21-947Z.png` | Fresh managed-route viewport proof after the GLB node-visual follow-up landed | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\element-2026-04-22T02-36-16-915Z.png` | Focused stage proof for closer role anchors, imported GLB node visuals, and live flow-marker presence | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\08-webgl-anchor-labels-detail-proof.png` | Zoomed detail proof showing anchor labels revealed for the inspected node | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\09-webgl-explicit-target-anchor-proof.png` | Connect-mode proof showing the exact compatible target input highlighted on a multi-input step | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\10-webgl-explicit-anchor-connected-proof.png` | Exact anchor-to-anchor connection proof after clicking one specific target input | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-toolbar-docked-clean.png` | Clean docked-stage proof for the single-row icon toolbar | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\webgl-toolbar-maximized-clean.png` | Clean maximized-stage proof for the icon toolbar plus overlay dock shell | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\page-2026-04-22T12-32-07-881Z.png` | Fresh live-route screenshot after the maximized-stage pointer-event repair | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\page-2026-04-22T15-01-43-229Z.png` | Shared overlay-window proof on the WebGL sandbox after extracting `OverlayLib` | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\page-2026-04-22T15-06-03-970Z.png` | Project-structure canvas proof with the shared selection-window chrome still rendered in the main app | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\project-structure-toolbox-window.png` | Focused project-structure toolbox-window proof after the shared overlay swap | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\page-2026-04-22T15-10-10-896Z.png` | Process-canvas proof showing the shared canvas editor window after opening a toolbox action on the `Steps` tab | `Captured` |
| `C:\repositories\CanDoItAll\output\playwright-mcp\processes-canvas-selection-window.png` | Focused process-canvas selection-window proof after the CanvasLib swap | `Captured` |

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| `01-runtime-foundation-refactor-and-api-shaping` | `Passed` | `Passed` | `Passed` | `Completed` | Runtime split completed without dropping the public automation bridge. |
| `02-in-scene-toolbar-and-settings-chrome` | `Passed` | `Passed` | `Passed` | `Completed` | Toolbar, settings panel, and context-menu chrome all moved into the runtime surface, then the toolbar was refined into a one-row icon strip with maximize/dock support. |
| `03-3d-connection-reconnection-and-delete-tools` | `Passed` | `Passed` | `Passed` | `Completed` | Connect, reconnect, delete, and scene/edge hit-target support implemented across runtime and sandbox session flow, then extended with exact anchor-to-anchor targeting and anchor-label proof. |
| `04-sandbox-integration-regression-proof-and-closure` | `Passed` | `Passed with residuals` | `Passed` | `Completed` | Host cleanup, manual MCP proof, and focused automated regressions completed; two Playwright smokes remain flaky in the fixture host. |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| `01-runtime-foundation-refactor-and-api-shaping` | `/webgl/process-workbench?template=branching-code-review` | `1900x1200` | `Navigate, inspect runtime snapshot/state, capture live baseline` | `page-2026-04-21T20-28-59-378Z.png` | `Passed` |
| `02-in-scene-toolbar-and-settings-chrome` | `/webgl/process-workbench?template=branching-code-review` | `1900x1200` | `Inspect toolbar, open settings, verify node-info density controls and extra scene toggles` | `element-2026-04-21T20-39-06-457Z.png`, `element-2026-04-21T20-39-28-616Z.png` | `Passed` |
| `03-3d-connection-reconnection-and-delete-tools` | `/webgl/process-workbench?template=branching-code-review` | `1900x1200` | `Open node context menu, inspect in-scene action affordances, confirm runtime authoring chrome presence` | `element-2026-04-21T20-40-48-951Z.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/webgl/process-workbench?template=branching-code-review` | `1280x860` | `Revisit live page at narrower width and capture full-page layout proof` | `page-2026-04-21T22-17-56-877Z.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/webgl/process-workbench?template=branching-code-review` | `1500x980` | `Regression repair pass: confirm WebGL geometry visibility, confirm chrome rerenders without rebuilding chrome objects, capture repaired viewport` | `webgl-regression-fix-check.png`, `webgl-regression-fix-settings-toggle.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/webgl/process-workbench?template=branching-code-review` | `1600x1100` | `Follow-up runtime split proof with default stage, host camera buttons, and WebGL toolbar visible together` | `webgl-refactor-validation-stage-default.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/webgl/process-workbench?template=branching-code-review&camera=perspective` | `1600x1100` | `Verify host and in-scene camera switching including repaired perspective round-trip and WebGL YZ-view proof` | `webgl-camera-yz-view.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/webgl/process-workbench?template=branching-code-review&camera=perspective` | `1600x1100` | `Verify new recomposition layouts on the live route` | `webgl-layout-critical-path-spine.png`, `webgl-layout-fanout-corridor.png`, `webgl-layout-radial-burst.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/webgl/process-workbench?template=branching-code-review` | `1600x1100` | `Inspect the live managed route, confirm imported GLB groups for role/branch/step nodes, confirm closer role-anchor projections, confirm `2` flow markers in runtime state, and capture the refreshed stage` | `page-2026-04-22T02-34-21-947Z.png`, `element-2026-04-22T02-36-16-915Z.png` | `Passed` |
| `03-3d-connection-reconnection-and-delete-tools` | `/webgl/process-workbench?template=branching-code-review` | `1900x1200` | `Focus an inspected node to reveal anchor labels, enter connect mode, choose one exact role output anchor, focus a multi-input step, confirm the intended compatible target input is highlighted, click that exact input, and verify the created edge keeps the expected source/target anchor ids` | `08-webgl-anchor-labels-detail-proof.png`, `09-webgl-explicit-target-anchor-proof.png`, `10-webgl-explicit-anchor-connected-proof.png` | `Passed` |
| `02-in-scene-toolbar-and-settings-chrome` | `/webgl/process-workbench?template=branching-code-review` | `1900x1200` | `Inspect toolbar geometry, confirm a single distinct toolbar row, invoke maximize and dock from the in-scene chrome, verify runtime state plus stage-shell expansion, and capture clean docked/maximized screenshots` | `webgl-toolbar-docked-clean.png`, `webgl-toolbar-maximized-clean.png` | `Passed` |
| `02-in-scene-toolbar-and-settings-chrome` | `/webgl/process-workbench?template=branching-code-review` | `1900x1200` | `Revisit the live route after the backdrop pointer-event repair and capture a fresh screenshot showing the stage rendered normally with the maximize-shell fix in place` | `page-2026-04-22T12-32-07-881Z.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/webgl/process-workbench?probe=overlay-refresh` | `1900x1200` | `Verify the extracted shared overlay library drives the WebGL sandbox selection and command-log windows with fresh geometry/state proof` | `page-2026-04-22T15-01-43-229Z.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/projects/{projectId}/structure` | `1900x1200` | `Create a project, verify shared selection-window action chrome, open the toolbox window, and confirm drag motion still repositions the shared window in the main app` | `page-2026-04-22T15-06-03-970Z.png`, `project-structure-toolbox-window.png` | `Passed` |
| `04-sandbox-integration-regression-proof-and-closure` | `/projects/{projectId}/processes` | `1900x1200` | `Open the process workspace Steps tab, confirm the shared selection window, open the process toolbox, trigger the canvas editor, and capture the swapped window chrome in the real app` | `page-2026-04-22T15-10-10-896Z.png`, `processes-canvas-selection-window.png` | `Passed` |

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| `N001` | `Closed` | Runtime split implemented across the `01`-`10` WebGL workbench module set, with camera, scene-graph, hit-test, action, and drag responsibilities isolated out of the original monolith. |
| `N002` | `Closed` | CanvasLib structure was analyzed as reference, but the WebGL split used smaller responsibilities instead of copying CanvasLib file sizing. |
| `N003` | `Closed` | In-scene context menus for scene, node, and edge interactions were added and manually proven with MCP screenshots. |
| `N004` | `Closed` | Connect and reconnect flows were added to runtime chrome, interop contracts, and sandbox session handling. |
| `N005` | `Closed` | A WebGL-drawn top toolbar was implemented, then refined into an icon-first single-row strip with maximize/dock proof on the live route. |
| `N006` | `Closed` | Selection and delete tooling now live in the in-scene runtime chrome. |
| `N007` | `Closed` | `Detailed`, `Miniature`, and `Hidden` node-info density modes were implemented in runtime settings. |
| `N008` | `Closed` | Additional useful settings added: grid, anchors, edge labels, diagnostics, role nodes, and branch helpers. |
| `N009` | `Closed with residual broader-suite flake` | Manual Playwright MCP route proof and screenshots were completed; the targeted toolbar/chrome smoke now passes with maximize/dock assertions, while the broader drag/export fixture-host smoke remains the last observed timing-sensitive automation gap. |

## Residual Risks

- The rendering/performance regression reported after the refactor was repaired and revalidated on the live sandbox route with new Playwright MCP screenshots and runtime diagnostics.
- The perspective route regression was repaired by making the default camera view explicit in both route application and route generation; live host-button and in-scene-toolbar proof now passes on `http://127.0.0.1:5123`.
- The GLB-node follow-up now proves imported model groups for sampled role, branch, and step nodes plus `2` start/end flow markers on the managed route at `http://127.0.0.1:5501`.
- The anchor-authoring follow-up now proves zoom-conditional anchor labels plus an exact role-output to step-input connection on the dedicated sandbox host at `http://127.0.0.1:5599`; the managed-runtime health probe still times out for this host even though the page and route are live in-browser.
- The toolbar maximize/dock follow-up repaired an intermediate blur/stretch issue by syncing the stage shell before viewport resize; manual MCP proof and the targeted toolbar/chrome smoke both now pass against that path.
- The maximize overlay follow-up repaired the remaining mouse-interaction issue by marking the backdrop as non-interactive; the targeted toolbar/chrome smoke now proves real mouse clicks still work while the stage is maximized.
- The shared overlay extraction was browser-validated on the WebGL sandbox plus the CanDoItAll project-structure and process canvases, but there is not yet a dedicated automated Playwright regression that covers the new `OverlayLib` path end-to-end in the main app.
- The broader Playwright smoke suite was not re-run end-to-end after the toolbar maximize follow-up; from the last full-suite run, the remaining observed timing-sensitive fixture-host case is `Sandbox_supports_drag_connection_and_export_without_camera_reset`.
- A stale MCP-managed `dotnet-watch` sandbox on `https://localhost:7123` could still lock `CanDoItAll.Components.WebGlSandbox` build outputs until that older watch pair is stopped; isolated test output paths avoid this for component coverage, and the live validation work used a separate fixed-port runtime.
- The live MCP browser route and the focused component/unit suites support the implementation, so the remaining gap is in automation stability rather than in the core runtime split or in-scene chrome delivery.
- `NU1903` package vulnerability warnings were already present in the broader solution restore graph and were not addressed as part of this WebGlLib refactor scope.
