# Hotspots

## Major hotspots

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` - 3227 lines
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` - 5001 lines

## Secondary hotspots

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs` - 532 lines
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.RichDefinitions.cs` - 529 lines
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCreateRequestComposer.cs` - 334 lines
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs` - 401 lines
- `src/CanDoItAll.Modules.Workspace/ProviderExecution.cs` - 294 lines
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs` - 515 lines

## Interpretation

The line counts alone are not the bug, but they correlate strongly with repeated unresolved architecture seams:
- too many reasons to change per file
- too many policy decisions hidden in orchestration hotspots
- too many future plugin-wave touchpoints concentrated in a few files
