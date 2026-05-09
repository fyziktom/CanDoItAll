# Current State

## Page Title

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor` currently renders `<PageTitle>Project Structure</PageTitle>`, so browser tabs do not identify the active project.
- The same page already has `surface.ProjectName` after load and an unavailable/loading state before `surface` exists.
- Existing helper patterns already truncate note titles with a substring plus `...`, so a small static title helper fits local style.

## Agent Context

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor` builds contextual prompts for project structure with selected project id only.
- `ProjectStructurePage.razor` tracks `selectedNodeIds` and selected nodes, but the contextual agent window component is not given those IDs.
- Therefore a user prompt such as "take selected nodes" reaches the agent without the concrete selected node IDs unless the agent independently reads UI state, which current tools do not expose.

## Typed Node Tooling

- `ProjectObjectType` includes `WorkItem`; `ProjectNodeKindRegistry` defines `WorkItem("task", "Task", ...)`, and the canvas catalog exposes `add-work-task`.
- `ProjectStructureNodeKindRequestJsonConverters.cs` already accepts `"task"` as a synthetic alias for `WorkItem/task`, but the MAF `project_structure_node_create` description mainly examples typed project blocks and file assets.
- `ProjectStructureAgentService.CreateNodeAsync` correctly normalizes subtypes through `ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType`, so the service can create task nodes if the agent passes the right type/subtype.
- There is no internal agent tool that returns the full node catalog and canonical create guidance in one call.

## Dependencies And Gantt Inputs

- `ProjectStructureAgentService.GetDependenciesAsync` builds prerequisite/dependent data through `ProjectStructureDependencyAnalyzer`.
- `ProjectStructureAgentApi` exposes `/dependencies/link`, `/dependencies/unlink`, and `/dependencies/query`.
- Existing integration tests prove dependency readiness and duration handling, but tool descriptions do not strongly tell agents to create task dependencies while adding task sets.

## Subproject Movement

- Existing UI/service support can move descendants of one source node to an existing target project.
- Existing transfer code rewrites moved roots to the target project root, preserves links where both endpoints move, and removes cross-project links.
- There is no service/API/MAF tool for the user story "selected nodes into a new subproject named XYZ"; agents would need many low-level steps and do not receive the selected-node IDs today.
