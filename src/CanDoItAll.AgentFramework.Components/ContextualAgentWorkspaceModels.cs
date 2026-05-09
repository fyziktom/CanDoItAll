using CanDoItAll.AgentFramework.Core;
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

public sealed record ContextualAgentWorkspaceRefreshRequest(
    ContextualAgentWorkspaceKind WorkspaceKind,
    Guid AgentId,
    Guid? ChatSessionId,
    Guid? ExecutionRunId,
    Guid? ProjectId = null,
    Guid? ProcessDefinitionId = null,
    IReadOnlyList<string>? SelectedNodeIds = null);

public static class ContextualAgentWorkspaceContextBuilder
{
    public static string BuildPrompt(
        ContextualAgentWorkspaceKind workspaceKind,
        Guid? projectId,
        Guid? processDefinitionId,
        IEnumerable<string>? selectedNodeIds,
        string prompt)
    {
        return workspaceKind switch
        {
            ContextualAgentWorkspaceKind.ProjectStructure when projectId.HasValue =>
                BuildProjectStructurePrompt(projectId.Value, selectedNodeIds, prompt),
            ContextualAgentWorkspaceKind.Processes when processDefinitionId.HasValue => $"""
Context:
- Workspace: process definition.
- Selected process definition id: {processDefinitionId.Value:D}.
- Treat "this process" and "selected process" as that process definition.
- Use process-definition operations for process reads or mutations.
- For adding one process role, use {AgentToolInvocationPolicyMetadata.ProcessesDefinitionRoleAdd} instead of loading and rewriting the full editor model.
- Do not use project-structure operations unless the user explicitly asks about project structure.

User request:
{prompt}
""",
            _ => prompt
        };
    }

    public static IReadOnlyList<string> NormalizeSelectedNodeIds(IEnumerable<string>? selectedNodeIds)
    {
        return selectedNodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? [];
    }

    private static string BuildProjectStructurePrompt(
        Guid projectId,
        IEnumerable<string>? selectedNodeIds,
        string prompt)
    {
        var normalizedSelectedNodeIds = NormalizeSelectedNodeIds(selectedNodeIds);
        var selectionLine = normalizedSelectedNodeIds.Count == 0
            ? "- Selected project-structure node ids: none."
            : $"- Selected project-structure node ids: {string.Join(", ", normalizedSelectedNodeIds)}.";

        return $"""
Context:
- Workspace: project structure.
- Selected project id: {projectId:D}.
{selectionLine}
- Treat "this project" and "selected project" as that project structure.
- Treat "selected nodes" as exactly the selected node ids listed above; if none are listed, ask for a selection or exact node ids.
- Use project-structure operations for structure reads or mutations.
- Use the project-structure node catalog before creating or reclassifying unfamiliar node kinds.
- When task ordering matters, create DependsOn dependency links so Gantt and readiness views stay correct.

User request:
{prompt}
""";
    }
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
