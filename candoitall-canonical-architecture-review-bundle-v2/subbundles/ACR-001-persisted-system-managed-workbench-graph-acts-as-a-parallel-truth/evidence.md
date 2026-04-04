# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:348-438 structure and calendar reads call SyncGraphAsync before reading workbench records
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1666-1943 SyncGraphAsync assembles expected nodes/links from Project, ProjectPhase, ProjectResource, PromptRun, PromptRunNode, ValidationRun, TestPlan, and ProjectHierarchyLink and persists them
- src/CanDoItAll.Modules.Workbench/ProjectGanttPreviewService.cs:10-17 Gantt preview is built from workbench structure output

## Root cause

A convenience projection cache gradually became the effective read model and therefore a second authoritative graph.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
