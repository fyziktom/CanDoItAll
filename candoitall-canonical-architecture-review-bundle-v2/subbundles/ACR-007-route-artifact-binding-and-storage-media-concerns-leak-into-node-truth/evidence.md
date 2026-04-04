# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1005-1013 MoveDescendantsToProjectAsync rewrites Route
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-60 ProjectObjectRecord includes route and file/media references
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:2552-2590 structure mapping exposes route/storage concerns directly

## Root cause

Convenient transport/navigation fields were stored with node truth instead of separate attachment/navigation models.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
