using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Components;

public enum ContextualAgentWorkspaceKind
{
    ProjectStructure,
    Processes
}

[Flags]
public enum ContextualAgentAccessLevel
{
    None = 0,
    Read = 1,
    Write = 2
}

public sealed record ContextualAgentAccessSummary(
    AgentDefinition Agent,
    ContextualAgentAccessLevel AccessLevel,
    string ScopeLabel)
{
    public bool CanRead => AccessLevel.HasFlag(ContextualAgentAccessLevel.Read);

    public bool CanWrite => AccessLevel.HasFlag(ContextualAgentAccessLevel.Write);
}

public static class ContextualAgentAccessResolver
{
    public static IReadOnlyList<ContextualAgentAccessSummary> Resolve(
        IEnumerable<AgentDefinition> agents,
        ContextualAgentWorkspaceKind workspaceKind,
        Guid? projectId = null,
        Guid? processDefinitionId = null)
    {
        ArgumentNullException.ThrowIfNull(agents);

        return agents
            .Where(agent => !agent.IsTemplate && agent.Status == AgentLifecycleStatus.Active)
            .Select(agent => Resolve(agent, workspaceKind, projectId, processDefinitionId))
            .Where(summary => summary is not null)
            .Cast<ContextualAgentAccessSummary>()
            .OrderBy(summary => summary.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ContextualAgentAccessSummary? Resolve(
        AgentDefinition agent,
        ContextualAgentWorkspaceKind workspaceKind,
        Guid? projectId,
        Guid? processDefinitionId)
    {
        return workspaceKind switch
        {
            ContextualAgentWorkspaceKind.ProjectStructure => ResolveProjectStructureAccess(agent, projectId),
            ContextualAgentWorkspaceKind.Processes => ResolveProcessAccess(agent, processDefinitionId),
            _ => null
        };
    }

    private static ContextualAgentAccessSummary? ResolveProjectStructureAccess(
        AgentDefinition agent,
        Guid? projectId)
    {
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        var accessLevel = ResolveAccessLevel(access.CanRead, access.CanWrite);
        if (accessLevel == ContextualAgentAccessLevel.None)
        {
            return null;
        }

        if (!access.AllowAllProjects)
        {
            if (projectId.HasValue && !access.AllowedProjectIds.Contains(projectId.Value))
            {
                return null;
            }

            if (!projectId.HasValue && access.AllowedProjectIds.Count == 0)
            {
                return null;
            }
        }

        var scopeLabel = access.AllowAllProjects
            ? "All projects"
            : projectId.HasValue
                ? "This project"
                : FormatScopeCount(access.AllowedProjectIds.Count, "project", "projects");

        return new ContextualAgentAccessSummary(agent, accessLevel, scopeLabel);
    }

    private static ContextualAgentAccessSummary? ResolveProcessAccess(
        AgentDefinition agent,
        Guid? processDefinitionId)
    {
        var access = AgentProcessAccessMetadata.Read(agent.ConfigurationJson);
        var accessLevel = ResolveAccessLevel(access.CanRead, access.CanWrite);
        if (accessLevel == ContextualAgentAccessLevel.None)
        {
            return null;
        }

        if (!access.AllowAllDefinitions)
        {
            if (processDefinitionId.HasValue && !access.AllowedDefinitionIds.Contains(processDefinitionId.Value))
            {
                return null;
            }

            if (!processDefinitionId.HasValue && access.AllowedDefinitionIds.Count == 0)
            {
                return null;
            }
        }

        var scopeLabel = access.AllowAllDefinitions
            ? "All processes"
            : processDefinitionId.HasValue
                ? "This process"
                : FormatScopeCount(access.AllowedDefinitionIds.Count, "process", "processes");

        return new ContextualAgentAccessSummary(agent, accessLevel, scopeLabel);
    }

    private static ContextualAgentAccessLevel ResolveAccessLevel(bool canRead, bool canWrite)
    {
        var accessLevel = ContextualAgentAccessLevel.None;
        if (canRead || canWrite)
        {
            accessLevel |= ContextualAgentAccessLevel.Read;
        }

        if (canWrite)
        {
            accessLevel |= ContextualAgentAccessLevel.Write;
        }

        return accessLevel;
    }

    private static string FormatScopeCount(int count, string singular, string plural)
        => count == 1 ? $"1 {singular}" : $"{count} {plural}";
}
