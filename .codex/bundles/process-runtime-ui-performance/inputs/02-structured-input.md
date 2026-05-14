# Structured Input

## Objectives

- Identify why observing multiple active process runs is expensive.
- Reduce repeated full-detail runtime reads in the Process Workspace refresh path.
- Keep process run execution, dispatch, transitions, artifact validation, and manager chat behavior intact.
- Prove the change with timing on both core code and browser interaction.

## Hard Constraints

- Preserve strongly typed C# models and service boundaries.
- Keep the Blazor component focused on orchestration, not data-access logic.
- Prefer focused read models over UI-side filtering of expensive full models.
- Do not introduce silent fallback behavior that hides failed reads or failed dispatch.

## Initial Current-State Signals

- `ProcessWorkspace.RefreshRuntimeWorkspaceAsync` reloads analytics, runtime overview, runtime pane data, canvas surface, and state every four seconds while the Runs or Analytics tab is active.
- `ProcessWorkspaceRunDetailsLoader.LoadActiveRunSummariesAsync` loops active runs and loads AgentFramework execution runs plus full `GetRunDetailsAsync` for each active run.
- `ProcessWorkspaceRunDetailsLoader.LoadExecutionRunsAsync` loads execution-run detail one run at a time for the selected run. That is acceptable for a selected detail pane, but too expensive for an active-run overview.
- AgentFramework `ListExecutionRunsAsync` filters after listing execution run records from storage, so repeated per-run calls multiply file reads.

## Assumptions

- The user cares about the process management page at `/processes` first, with project-scoped process pages as the same component surface.
- The highest impact fix is to make observation cheaper before changing runtime execution scheduling.
- Browser validation can use locally seeded process data rather than real LLM-backed process agents.
