using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Modules.Workbench.ProjectStructure;

public enum ProjectStructureAgentChatView
{
    Canvas,
    Gantt,
    Calendar
}

public static class ProjectStructureAgentChatContextBuilder
{
    public const string SourceKind = "project-structure";
    public const string BaseContributorId = "project-structure.guidance";
    public const string ViewContributorId = "project-structure.view";
    public const string SelectionContributorId = "project-structure.selection";

    public static AgentChatContextSource BuildSource(Guid projectId)
    {
        ValidateProjectId(projectId);
        return new AgentChatContextSource(
            new AgentChatContextSourceKind(SourceKind),
            new AgentChatContextSourceId(projectId.ToString("D")));
    }

    public static AgentChatContextScope BuildScope(
        AgentChatContextScopeId scopeId,
        Guid projectId,
        string projectName,
        IEnumerable<AgentDefinition> agents,
        AgentChatContextAccessState accessState = AgentChatContextAccessState.Ready,
        ProjectStructureAgentChatView activeView = ProjectStructureAgentChatView.Canvas,
        IReadOnlyList<AgentChatContextEntityReference>? selectedNodes = null)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ValidateProjectId(projectId);
        var access = ContextualAgentAccessResolver.Resolve(
                agents,
                ContextualAgentWorkspaceKind.ProjectStructure,
                projectId)
            .Select(item => new AgentChatContextAgentAccess(
                item.Agent.Id,
                ResolvePermissions(item),
                item.ScopeLabel))
            .ToArray();

        return new AgentChatContextScope(
            scopeId,
            BuildSource(projectId),
            BuildDisplayName(projectName, activeView),
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            access,
            AgentChatContextScopeAccessMode.AllowListed,
            accessState,
            BuildPosition(projectId, projectName, activeView, selectedNodes),
            completionRefreshMode: AgentChatContextCompletionRefreshMode.OnSuccessfulRun);
    }

    public static AgentChatSurfacePosition BuildPosition(
        Guid projectId,
        string projectName,
        ProjectStructureAgentChatView activeView,
        IReadOnlyList<AgentChatContextEntityReference>? selectedNodes)
    {
        ValidateProjectId(projectId);
        if (!Enum.IsDefined(activeView))
        {
            throw new ArgumentOutOfRangeException(nameof(activeView), activeView, "The project-structure agent-chat view is undefined.");
        }

        var normalizedProjectName = NormalizeProjectName(projectName);
        var selections = NormalizeSelectedNodes(selectedNodes);
        var (surface, view, route) = activeView switch
        {
            ProjectStructureAgentChatView.Canvas => (
                Surface: "project-structure",
                View: "canvas",
                Route: $"/projects/{projectId:D}/structure"),
            ProjectStructureAgentChatView.Gantt => (
                Surface: "project-structure",
                View: "gantt",
                Route: $"/projects/{projectId:D}/structure"),
            ProjectStructureAgentChatView.Calendar => (
                Surface: "project-calendar",
                View: "calendar",
                Route: $"/projects/{projectId:D}/calendar"),
            _ => throw new ArgumentOutOfRangeException(nameof(activeView), activeView, "The project-structure agent-chat view is undefined.")
        };

        return new AgentChatSurfacePosition(
            module: "projects",
            surface,
            view,
            route,
            primarySelection: new AgentChatContextEntityReference(
                "project",
                projectId.ToString("D"),
                normalizedProjectName),
            selectedEntities: selections);
    }

    public static AgentChatContextFragment BuildBaseFragment(Guid projectId)
    {
        ValidateProjectId(projectId);
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(BaseContributorId),
            order: 100,
            ContextualAgentWorkspaceContextBuilder.BuildProjectStructureBaseContext(projectId));
    }

    public static AgentChatContextFragment BuildSelectionFragment(
        IReadOnlyList<AgentChatContextEntityReference>? selectedNodes)
    {
        var normalizedSelections = NormalizeSelectedNodes(selectedNodes);
        var selectionLines = normalizedSelections.Count == 0
            ? "- Selected project-structure nodes: none."
            : string.Join(
                Environment.NewLine,
                normalizedSelections.Select(node => $"- Selected project-structure node: {node.Id} | {node.DisplayName}."));

        return new AgentChatContextFragment(
            new AgentChatContextContributorId(SelectionContributorId),
            order: 200,
            $"""
{selectionLines}
- Treat "selected nodes" as exactly the selected node ids and names listed above. If none are listed, work at selected project scope unless the request specifically requires a node selection.
""");
    }

    public static IReadOnlyList<AgentChatContextEntityReference> BuildSelectedNodes(
        IEnumerable<ProjectStructureNode>? selectedNodes)
    {
        return selectedNodes?
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .DistinctBy(node => node.Id, StringComparer.Ordinal)
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .Take(AgentChatPositionLimits.MaximumSelectedEntities)
            .Select(node => new AgentChatContextEntityReference(
                "project-node",
                node.Id,
                string.IsNullOrWhiteSpace(node.Title) ? node.Id : node.Title))
            .ToArray()
            ?? [];
    }

    public static IReadOnlyList<AgentChatContextEntityReference> BuildSelectedEntities(
        IEnumerable<ProjectStructureNode>? selectedNodes,
        IEnumerable<AgentChatContextEntityReference>? selectedEntities)
    {
        var nodeSelections = BuildSelectedNodes(selectedNodes);
        return NormalizeSelectedNodes(
            [.. nodeSelections, .. selectedEntities ?? []]);
    }

    public static AgentChatContextFragment BuildViewFragment(
        ProjectStructureAgentChatView view)
    {
        if (!Enum.IsDefined(view))
        {
            throw new ArgumentOutOfRangeException(nameof(view), view, "The project-structure agent-chat view is undefined.");
        }

        var content = view switch
        {
            ProjectStructureAgentChatView.Canvas => """
Current project workspace view: structure canvas.
- The visible surface is the interactive project-structure canvas.
- Selected project-structure node ids are supplied by the separate selection fragment.
""",
            ProjectStructureAgentChatView.Gantt => """
Current project workspace view: Gantt schedule.
- The visible surface is the interactive Gantt schedule for the selected project.
- The Gantt UI does not currently expose an individual task selection to agent chat; work at project schedule scope unless the user names a task.
""",
            ProjectStructureAgentChatView.Calendar => """
Current project workspace view: project calendar.
- The visible surface is the calendar of scheduled project-structure nodes.
- A selected calendar event is supplied as its canonical project-structure node key by the separate selection fragment.
""",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The project-structure agent-chat view is undefined.")
        };

        return new AgentChatContextFragment(
            new AgentChatContextContributorId(ViewContributorId),
            order: 150,
            content);
    }

    private static AgentChatContextPermission ResolvePermissions(
        ContextualAgentAccessSummary access)
    {
        var permissions = AgentChatContextPermission.Read;
        if (access.CanWrite ||
            access.CanWriteTasks ||
            access.CanWriteNonTaskStructure ||
            access.CanCreateProjects ||
            access.CanCreateSubprojects)
        {
            permissions |= AgentChatContextPermission.Mutate;
        }

        return permissions;
    }

    private static string BuildDisplayName(
        string projectName,
        ProjectStructureAgentChatView activeView)
    {
        var surfaceName = activeView == ProjectStructureAgentChatView.Calendar
            ? "Project calendar"
            : "Project structure";
        return $"{surfaceName} · {NormalizeProjectName(projectName)}";
    }

    private static string NormalizeProjectName(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        return projectName.Trim();
    }

    private static IReadOnlyList<AgentChatContextEntityReference> NormalizeSelectedNodes(
        IReadOnlyList<AgentChatContextEntityReference>? selectedNodes)
    {
        return selectedNodes?
            .Where(node => node is not null && string.Equals(node.Kind, "project-node", StringComparison.Ordinal))
            .DistinctBy(node => node.Id, StringComparer.Ordinal)
            .OrderBy(node => node.Id, StringComparer.Ordinal)
            .Take(AgentChatPositionLimits.MaximumSelectedEntities)
            .ToArray()
            ?? [];
    }

    private static void ValidateProjectId(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }
    }
}
