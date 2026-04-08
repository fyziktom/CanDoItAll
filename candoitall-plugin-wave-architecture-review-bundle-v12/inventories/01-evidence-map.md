# Evidence map

## Phase10 regression evidence in current upload
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:135`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:167-175`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:361-388`

## Missing phase10 recovery boundary in current upload
- `src/CanDoItAll.Modules.Workbench/ProjectStructureProjectionMaintenanceService.cs` — missing
- `src/CanDoItAll.Modules.Workbench/WorkbenchModuleServiceCollectionExtensions.cs` — missing registration

## Shared editor regression evidence in current upload
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor:273-276`
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs:326-333`
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs:209-219`
- `src/CanDoItAll.Modules.Workspace/Pages/Components/ConnectorConfigFieldEditor.razor:16-23`

## Runtime-plane gap evidence in current upload
- `src/CanDoItAll.Modules.Automation/AutomationModels.cs:10-24`
- `src/CanDoItAll.Modules.Automation/AutomationModuleServiceCollectionExtensions.cs:9-13`
- `src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs:15-20,93-101,120-141`
- `src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:98-100`
- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs:326-354`
- `src/CanDoItAll.Web/Program.cs:38-63`

## Regression vs previous upload
- see `inventories/06-regression-diff-vs-previous-upload.txt`
- see `inventories/05-phase10-gate-previous-upload-run.txt`
- see `inventories/02-phase10-gate-current-run.txt`
