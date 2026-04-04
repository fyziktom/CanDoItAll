# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:52-53 ProjectObjectRecord stores PositionX / PositionY
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:143-176 ProjectStructureNode exposes X, Y, legacy marker columns, and marker collections together
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:557-616 metadata serializer resolves marker sets and primary marker
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1399-1404 ApplyPrimaryMarker writes legacy marker columns from the marker set
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:113-130 ProjectWorkbenchViewStateRecord keeps separate viewport/state JSON, showing that some UI state is already distinct

## Root cause

The system correctly kept spatial data near nodes, but signal semantics and view semantics never received a dedicated canonical model boundary.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
