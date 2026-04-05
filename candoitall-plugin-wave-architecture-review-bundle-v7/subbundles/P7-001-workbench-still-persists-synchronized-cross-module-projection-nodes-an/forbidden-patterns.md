# Forbidden patterns

The following patterns must be removed or made impossible:
- private async Task SyncGraphAsync
- await SyncGraphAsync(
- IsSystemManaged = true for cross-module projection nodes in Workbench canonical tables

## Evidence anchors
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:350-388
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:398-425
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1962-2239
