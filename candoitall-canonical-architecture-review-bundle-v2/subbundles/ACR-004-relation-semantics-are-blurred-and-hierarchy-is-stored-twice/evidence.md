# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:441-502 CreateObjectAsync writes ParentNodeKey and UpsertLinkAsync
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:598-643 ReparentObjectAsync updates ParentNodeKey and recreates link rows
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1990-1993 ResolveHierarchyLinkKind maps hierarchy through generic link kinds
- src/CanDoItAll.Modules.Workbench/ProjectStructureDependencyAnalysis.cs:81-125 prerequisites include ancestors, explicit DependsOn, and inverse Blocks

## Root cause

Tree placement, dependency edges, and execution semantics evolved together without a strict relation taxonomy and one canonical hierarchy owner.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
