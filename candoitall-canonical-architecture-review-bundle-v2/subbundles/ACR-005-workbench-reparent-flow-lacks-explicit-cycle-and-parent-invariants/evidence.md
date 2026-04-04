# Evidence

## Code evidence

- src/CanDoItAll.Modules.Projects/ProjectModels.cs:748-795 ValidateHierarchyConnectionAsync rejects self-parent and descendant cycles for project hierarchy
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:598-643 ReparentObjectAsync mutates parent/link state without visible cycle validation
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:1949-1952 NormalizeEditableParentNodeKey only normalizes null/whitespace

## Root cause

Invariant logic exists in one canonical area but was not promoted into a shared structure mutation policy for workbench node reparenting.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
