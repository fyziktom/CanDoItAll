# Current State

## Confirmed Owners

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
  - owns the selection panel, inspector actions, and local-open feedback rendering
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureNodeDescriptor.cs`
  - owns typed selection-panel facts for script and environment nodes
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
  - owns the typed metadata shapes and enums for script/environment nodes
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCreateRequestComposer.cs`
  - maps typed create-definition fields into script/environment metadata
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`
  - defines the create fields available for script and environment nodes
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureLocalFileOpener.cs`
  - shows the existing module-local service pattern for trusted local OS actions
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\WorkbenchModuleServiceCollectionExtensions.cs`
  - owns DI registration for workbench services

## Verified Findings

- The selection panel already has a `Node actions` section, but it only exposes routed node commands plus canvas graph actions.
- The existing `Open` node command flow is built around artifact references and page navigation, not local process creation.
- Script nodes already store the command, arguments, and working directory needed for predictable process launch.
- Environment nodes already store typed metadata for dotnet watch, other dotnet runtime modes, and python environments.
- There is no existing service in the workbench module that can resolve a node into a PowerShell launch plan and execute it locally.

## Existing Test Surface

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkspaceRuntimeProcessToolsTests.cs`
