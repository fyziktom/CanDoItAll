
# File references

## Existing files to inspect first

- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchSchemaInitializer.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Likely new files or folders

- `src/CanDoItAll.Modules.Workbench/ProjectObjectMetadata/*.cs`
- `tests/CanDoItAll.Tests.Unit/ProjectObjectMetadataTests.cs`

## Reuse guidance

- Prefer modifying existing modules and shared components before creating new parallel systems.
- Keep new files cohesive and small; do not scatter item logic across unrelated modules without a reason.
- When a file from another item is reused, preserve its shared nature and avoid item-specific hacks.
