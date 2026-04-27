# Current State

## Investigation Sources

- CodeAnalytics snapshot: `snap-20260427131012-9bdb58b8`
- Live app watch: healthy at `https://localhost:7271`
- Local search fallback: `rg` is unavailable in this workspace with access denied, so Git/PowerShell searches were used.

## Existing Transfer System

The existing database transfer contract lives in:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseTransferModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseTransferService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Database\DatabaseProfileWorkspaceService.cs`

Registered transfer handlers already include:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\DatabaseTransfer\ProjectStructureMcpDatabaseTransferHandler.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\DatabaseTransfer\AiProvidersDatabaseTransferHandler.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\DatabaseTransfer\AiAgentsDatabaseTransferHandler.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\DatabaseTransfer\ProcessDefinitionsDatabaseTransferHandler.cs`

The create-empty/new-database transfer prompt and data-source transfer dialog consume the same handler list. Adding a `Projects` handler automatically gives the existing UI another transfer item if the handler is registered in DI.

## Existing Zip/Snapshot Support

Whole-database snapshot zip support exists in:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseSnapshots.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`

That system exports all database tables. The requested feature is narrower: all projects. It should not force the user to clone the whole runtime database when they only asked for project import/export.

## Project Data Inventory

Core project records:

- `Projects_Projects` via `Project`
- `Projects_ProjectPhases` via `ProjectPhase`
- `Projects_ProjectOptionSelections` via `ProjectOptionSelection`
- `Projects_ProjectHierarchyLinks` via `ProjectHierarchyLink`

Workbench/project-structure records:

- `Workbench_ProjectObjects` via `ProjectObjectRecord`
- `Workbench_ProjectObjectLinks` via `ProjectObjectLinkRecord`
- `Workbench_ProjectProjectionLayouts` via `ProjectStructureProjectionLayoutRecord`
- `Workbench_ProjectNodeBindings` via `ProjectNodeBindingRecord`
- `Workbench_ProjectNodeReferences` via `ProjectNodeReferenceRecord`
- `Workbench_ProjectNodeLifecycleEvents` via `ProjectNodeLifecycleEventRecord`
- `Workbench_ProjectCrossModuleMutations` via `ProjectCrossModuleMutationRecord`
- `Workbench_ViewStates` via `ProjectWorkbenchViewStateRecord`

Volatile or runtime-adjacent project structure tables:

- `Workbench_ProjectStructureLeases`
- `Workbench_ProjectStructureOperationAnalytics`

These should not be treated as project content by default.

## UI Surfaces

- Existing database transfer modal: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor`
- Existing startup/new managed SQLite transfer prompt: `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor`
- Projects board route: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- Projects board component: `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor`

## Existing Test Targets

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\DatabaseRuntimeSwitchingIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectsServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectsPageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\MainLayoutDatabaseProfileTests.cs`
