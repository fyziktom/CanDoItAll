# Evidence

## Code evidence

- src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:314-2931 ProjectWorkbenchService spans graph sync, mutations, view state, mapping, and helper logic
- src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.PartyIntegration.cs depends on ProjectWorkbenchService + ProjectPartyIntegrationBridge for one cohesive workflow
- inventory: Modules.Workbench is referenced by Web, Composition, MCP.ProjectStructure, Tests.Support, Tests.Unit, Tests.Components, and CRM/HR-adjacent pages

## Root cause

Wave-by-wave growth concentrated orchestration into one central service rather than layered collaborators.

## Architectural interpretation

This finding exists because the current code lets one concern occupy too many roles at once.
