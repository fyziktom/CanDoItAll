
# File references

## Existing files to inspect first

- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePlacementPolicyTests.cs`

## Likely new files or folders

- `tests/CanDoItAll.Tests.Playwright/ProjectStructurePlacementVisualTests.cs`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
