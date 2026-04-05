# Forbidden patterns

The following patterns must be removed or made impossible:
- ResolveHierarchyLinkKind(
- UpsertLinkAsync(... ProjectObjectLinkKind.Contains ...)
- UpsertLinkAsync(... ProjectObjectLinkKind.BelongsTo ...)

## Evidence anchors
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:447-499
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:626-650
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1059-1068
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:2286-2289
- src/CanDoItAll.Modules.Workbench/ProjectStructureInvariantService.cs:56-74
