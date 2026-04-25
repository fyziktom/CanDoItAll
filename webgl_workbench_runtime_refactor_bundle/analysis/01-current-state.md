# Current State

## Runtime Shape

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\wwwroot\js\runtime\workbench\01-webgl-workbench.js` is currently the only workbench runtime file in WebGlLib.
- The file is about `63 KB` and `1828` lines long.
- It currently owns unrelated responsibilities in one place: state creation, camera math, scene rebuilds, node and edge rendering, DOM label mirroring, drag logic, selection, connection simulation, image export, diagnostics, and the public `window.CanDoItAll.webglWorkbench` API.

## Existing WebGL Chrome And Missing Chrome

- The runtime host shell currently creates only the stage, DOM mirror layers, empty state, and a diagnostics panel.
- The runtime explicitly suppresses the browser context menu with `event.preventDefault()` but does not replace it with a custom right-click menu.
- There is no in-scene toolbar in the runtime today.
- There is no explicit tool-mode state for select, delete, connect, or reconnect.
- The runtime already supports click selection and shift-drag node movement, but it does not expose delete or reconnect flows as stage-local tools.

## Surface Contract Gaps

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchUiState.cs` currently stores selected ids, view preset, layout mode, spacing, deterministic mode, diagnostics, and camera state.
- That UI state does not yet model tool mode, node info density, grid visibility, anchor visibility, edge-label visibility, or context-menu state.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchEvents.cs` currently covers selection, node movement, and connection change requests only.
- There is no delete request contract today.

## Sandbox Host State

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\Components\Pages\ProcessWorkbench.razor` currently renders many stage-driving controls outside the WebGL surface:
- template, view preset, and layout selectors are host HTML controls
- spacing, recompose, reset, fit view, diagnostics, and export actions are host HTML buttons
- connection and disconnection are currently driven by a host-side semantic form
- a floating HTML overlay currently drives orbit, pan, zoom, and reset camera interactions
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\wwwroot\webgl-sandbox.css` styles the HTML stage overlay, which means current proof still depends on non-WebGL chrome.

## Sandbox Session And Mutations

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\ProcessWebGlSandboxSession.cs` already supports in-memory node-position overrides, layout recomposition, spacing changes, diagnostics toggling, export logging, and connection mutation via `ProcessWebGlSceneAdapter`.
- The sandbox session does not yet expose a delete flow.
- The existing connection flow is model-aware and routes through `ProcessWebGlSceneAdapter.ApplyConnectionChange(...)`, which is valuable to preserve.

## CanvasLib Comparison

- CanvasLib's workbench runtime is split into multiple files such as:
- `01-foundation.js`
- `03a-context-menu-shortcuts.js`
- `04-context-menu-and-composer.js`
- `07-runtime-entry.js`
- CanvasLib still has several very large files, but it already separates foundation concerns, menu logic, entry wiring, and rendering/interaction layers instead of placing all behavior in one file.
- That split is the closest local example for this refactor, but WebGlLib needs a cleaner first pass because its current surface is still much smaller and more self-contained.

## Existing Proof Surface

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\WebGlSandboxSmokeTests.cs` already proves rendering, template switching, drag, connection mutation, navigation controls, spacing changes, label scaling, collision protection, and responsive visibility.
- Those Playwright tests currently target host HTML controls such as the overlay navigation buttons and the connection form, so they will need intentional updates when the authoring chrome moves into the stage.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\WebGlWorkbenchInteropTests.cs`, `ProcessWebGlSandboxSessionTests.cs`, `ProcessWebGlSceneAdapterTests.cs`, and `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WebGlWorkbenchUiStateTests.cs` provide a reasonable .NET-side regression base for the surface and session contracts.

## Related Existing Bundle

- `C:\repositories\CanDoItAll\webgl_process_workbench_concept_bundle` documents the earlier concept delivery and proof loop.
- That bundle is useful as historical context, but it is not a substitute for this refactor-and-gap-closure bundle because the current request is narrower, more code-structure-focused, and explicitly asks for new in-scene authoring chrome.
