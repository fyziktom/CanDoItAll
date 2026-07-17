using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench.ProjectStructure;

public enum ProjectStructureAgentChatView
{
    Canvas,
    Gantt
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
        AgentChatContextAccessState accessState = AgentChatContextAccessState.Ready)
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
            BuildDisplayName(projectName),
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            access,
            AgentChatContextScopeAccessMode.AllowListed,
            accessState);
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
        IEnumerable<string>? selectedNodeIds)
    {
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(SelectionContributorId),
            order: 200,
            ContextualAgentWorkspaceContextBuilder.BuildProjectStructureSelectionContext(selectedNodeIds));
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

    private static string BuildDisplayName(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        return $"Project structure · {projectName.Trim()}";
    }

    private static void ValidateProjectId(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }
    }
}
