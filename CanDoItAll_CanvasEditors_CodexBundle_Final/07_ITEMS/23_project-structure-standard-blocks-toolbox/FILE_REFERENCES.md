
# File references

## Existing files to inspect first

- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Likely new files or folders

- `tests/CanDoItAll.Tests.Playwright/ProjectStructureToolboxVisualTests.cs`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
