
# File references

## Existing files to inspect first

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Likely new files or folders

- `tests/CanDoItAll.Tests.Components/ProgressSummaryModalTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProgressExportTests.cs`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
