
# File references

## Existing files to inspect first

- `src/CanDoItAll.ComponentKit/Components/FloatingInspectorHost.razor`
- `src/CanDoItAll.ComponentKit/Canvas/Graph/FloatingInspectorHost.cs`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor.css`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
- `tests/CanDoItAll.Tests.Components/FloatingInspectorHostTests.cs`

## Likely new files or folders

- `tests/CanDoItAll.Tests.Components/FloatingToolWindowHostTests.cs`
- `tests/CanDoItAll.Tests.Playwright/FloatingToolWindowVisualTests.cs`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
