# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:396-438 calendar read path depends on SyncGraphAsync and workbench records
- src/CanDoItAll.Modules.Workbench/ProjectGanttPreviewService.cs:10-17 Gantt preview uses workbench structure
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:2552-2590 structure node mapping mixes metadata, signals, routes, storage, and schedule info

## Root cause

Convenient reuse of one persisted structure DTO turned projections into chained read models.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
