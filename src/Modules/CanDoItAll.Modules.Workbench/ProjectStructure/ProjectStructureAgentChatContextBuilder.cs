using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench.ProjectStructure;

public static class ProjectStructureAgentChatContextBuilder
{
    public const string SourceKind = "project-structure";
    public const string BaseContributorId = "project-structure.guidance";
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

    private static AgentChatContextPermission ResolvePermissions(
        ContextualAgentAccessSummary access)
    {
        var permissions = AgentChatContextPermission.Read;
        if (access.CanWrite || access.CanWriteTasks || access.CanWriteNonTaskStructure)
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
