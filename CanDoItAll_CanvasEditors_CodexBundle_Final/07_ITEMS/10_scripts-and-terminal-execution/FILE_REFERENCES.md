
# File references

## Existing files to inspect first

- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `tools/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `tests/CanDoItAll.Tests.Unit/WorkspaceRuntimeProcessToolsTests.cs`

## Likely new files or folders

- `src/CanDoItAll.Modules.Workbench/Runtime/TerminalSessionModels.cs`
- `tests/CanDoItAll.Tests.Components/ScriptNodeExecutionTests.cs`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
