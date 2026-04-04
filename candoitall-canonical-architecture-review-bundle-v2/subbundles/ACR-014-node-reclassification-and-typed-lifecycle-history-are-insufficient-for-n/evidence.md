# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:896-927 ReclassifyObjectAsync mutates the same ProjectObjectRecord in place
- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:2352-2358 IsSupportedReclassification only allows ProjectBlock->ProjectBlock and Note->ProjectBlock
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeMutations.cs:12-17 and 107-125 UI currently exposes note→block and block-type change only
- User clarification captured in inputs/02-user-clarifications.md: the core workflow is brainstorming via simple nodes that later become richer typed blocks/tasks/decisions, and preserving that reasoning history matters

## Root cause

Node lifecycle was modeled as a simple type mutation rather than a first-class transition/facet history over a stable node carrier.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
