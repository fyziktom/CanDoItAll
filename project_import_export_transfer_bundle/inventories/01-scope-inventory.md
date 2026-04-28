# Scope Inventory

## Source Files To Edit

| Area | File |
| --- | --- |
| Project transfer handler registration | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Services\WorkbenchModuleServiceCollectionExtensions.cs` |
| Project transfer implementation | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\DatabaseTransfer\ProjectsDatabaseTransferHandler.cs` |
| Project package service | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\DatabaseTransfer\ProjectPackageService.cs` |
| Project package models | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\DatabaseTransfer\ProjectPackageModels.cs` |
| Projects page UI | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor` |
| Projects board UI | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\Components\ProjectsBoard.razor` |

## Existing UI That Should Gain Projects Automatically

| Area | File |
| --- | --- |
| Data sources transfer dialog | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\DatabaseSourcesSettingsPanel.razor` |
| Startup/new managed SQLite transfer prompt | `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayoutDatabaseDialog.razor` |

## Tests To Add Or Update

| Test scope | File |
| --- | --- |
| Project transfer integration | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\DatabaseRuntimeSwitchingIntegrationTests.cs` or new focused integration test file |
| Project package import/export integration | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectPackageTransferIntegrationTests.cs` |
| Project UI controls | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectsPageTests.cs` |
| Existing transfer UI item | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\MainLayoutDatabaseProfileTests.cs` or data-source component tests |

## Tables In Project Transfer Scope

- `Projects_Projects`
- `Projects_ProjectPhases`
- `Projects_ProjectOptionSelections`
- `Projects_ProjectHierarchyLinks`
- `Workbench_ProjectObjects`
- `Workbench_ProjectObjectLinks`
- `Workbench_ProjectProjectionLayouts`
- `Workbench_ProjectNodeBindings`
- `Workbench_ProjectNodeReferences`
- `Workbench_ProjectNodeLifecycleEvents`
- `Workbench_ProjectCrossModuleMutations`
- `Workbench_ViewStates`

## Explicitly Out Of Project Package Scope

- `Workbench_ProjectStructureLeases`
- `Workbench_ProjectStructureOperationAnalytics`
- process runtime run history
- AI agent execution history
- whole database snapshots
