# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:26-60 ProjectObjectRecord contains domain, spatial, schedule, storage, route, and signal fields in one type
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:143-176 ProjectStructureNode exposes many mixed concerns from one source record
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs:165-190 metadata envelope packs multiple optional families
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs:240-406 party integration writes CRM/HR-linked metadata back into the same node payload

## Root cause

Fast feature waves were absorbed by one universal node record instead of stable companion concepts and typed facets.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
