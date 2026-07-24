# Scope Inventory

| Source | Current responsibility | Target owner | Risk |
| --- | --- | --- | --- |
| `Pages/ProjectStructurePage.Processes.cs` | context summary, visual-target rules, redaction, output-root resolution/aliases | `ProjectStructureProcessLaunchContextBuilder` | high |
| `ProjectStructure/ProjectStructureProcessNodeService.cs` | duplicate of the same launch-context policy | `ProjectStructureProcessLaunchContextBuilder` | high |
| `Pages/ProjectStructurePage.ProjectHierarchy.cs` | candidate filtering and graph cycle traversal | `ProjectStructureProjectHierarchySelectionPolicy` | medium |
| `Pages/ProjectStructurePage.razor` and all other partials | UI state, orchestration, unrelated capabilities | unchanged | protected |
| `Workbench/ProjectWorkbenchModels.cs` and persistence | canonical/projected graph models and persistence | unchanged | protected |

## Test Ownership

- new direct unit tests: launch-context builder, hierarchy selection policy;
- new architecture checkpoint: both callers delegate, duplicate methods are absent, page partial count does not grow;
- existing component regression: page simple mutations, moves, database switching, task assignment creation, web preview;
- existing integration characterization: agent-started process launch context, environment permitting.
