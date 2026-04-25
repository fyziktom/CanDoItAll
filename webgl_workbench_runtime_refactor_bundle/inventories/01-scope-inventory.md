# Scope Inventory

## Primary Runtime Files

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\wwwroot\js\runtime\workbench\01-webgl-workbench.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\wwwroot\css\workbench\webgl-workbench.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchSurface.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchUiState.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\WebGl\WebGlWorkbenchEvents.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlLib\Components\Workbench\WebGlWorkbench.razor`

## Sandbox Host Files

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\Components\Pages\ProcessWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\ProcessWebGlSandboxSession.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\wwwroot\webgl-sandbox.css`

## CanvasLib Comparison Files

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\01-foundation.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\03a-context-menu-shortcuts.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js`

## Existing Automated Proof Files

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWebGlSandboxSessionTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWebGlSceneAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\WebGlWorkbenchInteropTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WebGlWorkbenchUiStateTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\WebGlSandboxSmokeTests.cs`

## Supporting Tooling

- `C:\repositories\CanDoItAll\tools\webgllib\asset-manifest.json`
- `C:\repositories\CanDoItAll\tools\webgllib\build-assets.cjs`
- `C:\repositories\CanDoItAll\tools\webgllib\verify-assets.cjs`
- `C:\repositories\CanDoItAll\package.json`

## Initial Gap Inventory

- Runtime split gap: the current WebGl runtime is one file.
- Chrome gap: no stage-local toolbar and no stage-local right-click menu.
- Tool gap: no stage-local delete tool and no reconnect flow.
- Settings gap: no explicit node-info density setting and no extra visibility settings beyond diagnostics.
- Proof gap: Playwright coverage currently points at old host HTML controls and must move with the UI.
