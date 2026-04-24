# Source Artifacts

- User request in the current Codex thread on 2026-04-23.
- Existing shared overlay library source: `C:\repositories\CanDoItAll\src\CanDoItAll.Components.OverlayLib`.
- Existing CanvasLib floating window adapter: `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor`.
- Existing project structure toolbox: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolboxWindow.razor`.
- Existing process canvas toolbox: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolboxWindow.razor`.
- Existing prompt factory toolbox markup: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`.
- Existing WebGL sandbox page: `C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\Components\Pages\ProcessWorkbench.razor`.

## Component MCP Findings

- `CanvasFloatingWindow` is cataloged as the shared CanvasLib floating window surface for auxiliary panels inside the workbench runtime.
- Real usages already include prompt factory, process canvas, project structure health/signals/toolbox, and the Canvas sandbox.
- The catalog did not expose a ready generic component toolbox; the reusable layer should therefore live beside `OverlayWindow` and be consumed by CanvasLib/WebGL hosts.
