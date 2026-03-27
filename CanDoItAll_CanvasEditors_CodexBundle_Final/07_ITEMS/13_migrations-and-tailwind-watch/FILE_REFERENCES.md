
# File references

## Existing files to inspect first

- `tools/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `tests/CanDoItAll.Tests.Unit/WorkspaceRuntimeProcessToolsTests.cs`

## Likely new files or folders

- `tests/CanDoItAll.Tests.Components/MigrationAndTailwindNodeTests.cs`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
