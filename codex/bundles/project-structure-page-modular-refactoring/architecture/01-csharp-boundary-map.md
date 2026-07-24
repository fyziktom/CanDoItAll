# C# Boundary Map

## Current Owners

- `ProjectStructurePage.Processes.cs` and `ProjectStructureProcessNodeService.cs` both own process-launch context policy.
- `ProjectStructurePage.ProjectHierarchy.cs` owns UI orchestration and graph eligibility policy together.

## Target Owners

### `ProjectStructureProcessLaunchContextBuilder`

- top-level internal concrete type in the existing Workbench `ProjectStructure` folder;
- builds an immutable launch-context result from a read-only surface and focus node;
- owns summary traversal, filtering, limits, visual-target selection, redaction, output-root resolution, and alias application;
- is called by both UI and agent process launch paths.

### `ProjectStructureProjectHierarchySelectionPolicy`

- top-level internal static policy in the existing Workbench `ProjectStructure` folder;
- owns attach/reconnect eligibility and graph traversal;
- does not depend on page dialog state or its UI enum.

## Page Responsibilities Left In Place

- open/close dialogs, feedback, selection, loading lists, invoking application services, and reloading surface state;
- all unrelated Project Structure capabilities.

## Rejected Boundaries

- No `IProjectStructureProcessLaunchContextBuilder`: there is one deterministic algorithm and no lifecycle/provider boundary.
- No new project: both callers and models already live in the same project, so a project split would add references without SDK isolation.
- No action strategies or page-state facade: they would require a god context/callback bag and preserve the monolith.
- No new partial or nested class.

## Migration

1. Add direct tests for the new owner contract.
2. Move the duplicated implementation once.
3. Replace both old paths with direct calls.
4. Delete duplicate methods/usings.
5. Extract hierarchy policy and replace the page predicates.
6. Run source assertions and regression gates.
