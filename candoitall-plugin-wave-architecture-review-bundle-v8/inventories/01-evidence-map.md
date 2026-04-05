## Evidence map

### Strong improvements confirmed
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs` includes tests that external artifacts appear in structure/calendar surfaces without persisting mirrored workbench rows.
- `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs` assembles projections in memory.
- `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs` introduces binding/reference persistence.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchLifecycleService.cs` + `src/CanDoItAll.Modules.Workbench/ProjectNodeLifecycleHistory.cs` record note-promotion and subtype-change events.
- `src/CanDoItAll.Modules.Workspace/ConnectorPluginPlatform.cs` and `src/CanDoItAll.Modules.Resources/ResourceConnectorPlugins.cs` establish a real connector-manifest foundation.

### Remaining blockers
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:36-43, 73-79`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchRelationService.cs:83-110`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:461-466, 528-533`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:476-500`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4894-4933`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:223, 234, 236, 244, 246, 290, 298, 300, 391, 447, 476, 545-557`
- `src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor:252-253`
- `src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor:48, 139-190, 425`
- `src/CanDoItAll.Modules.Workspace/ProviderExecution.cs:60-83`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutationService.cs:98-109, 278-296`
