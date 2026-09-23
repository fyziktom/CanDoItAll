using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

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
    Write = 2,
    TaskWrite = 4,
    NonTaskStructureWrite = 8,
    ProjectCreate = 16,
    SubprojectCreate = 32
}

public sealed record ContextualAgentAccessSummary(
    AgentDefinition Agent,
    ContextualAgentAccessLevel AccessLevel,
    string ScopeLabel)
{
    private const ContextualAgentAccessLevel MutationAccessLevels =
        ContextualAgentAccessLevel.Write |
        ContextualAgentAccessLevel.TaskWrite |
        ContextualAgentAccessLevel.NonTaskStructureWrite |
        ContextualAgentAccessLevel.ProjectCreate |
        ContextualAgentAccessLevel.SubprojectCreate;

    public bool CanRead => AccessLevel.HasFlag(ContextualAgentAccessLevel.Read);

    public bool CanWrite => AccessLevel.HasFlag(ContextualAgentAccessLevel.Write);

    public bool CanWriteTasks => AccessLevel.HasFlag(ContextualAgentAccessLevel.TaskWrite);

    public bool CanWriteNonTaskStructure => AccessLevel.HasFlag(ContextualAgentAccessLevel.NonTaskStructureWrite);

    public bool CanCreateProjects => AccessLevel.HasFlag(ContextualAgentAccessLevel.ProjectCreate);

    public bool CanCreateSubprojects => AccessLevel.HasFlag(ContextualAgentAccessLevel.SubprojectCreate);

    public bool CanMutate => (AccessLevel & MutationAccessLevels) != ContextualAgentAccessLevel.None;
}

public sealed record ContextualAgentWorkspaceRefreshRequest(
    ContextualAgentWorkspaceKind WorkspaceKind,
    Guid AgentId,
    Guid? ChatSessionId,
    Guid? ExecutionRunId,
    Guid? ProjectId = null,
    Guid? ProcessDefinitionId = null,
    IReadOnlyList<string>? SelectedNodeIds = null);

public static class ContextualAgentWorkspaceContextBuilder {
    public static string BuildPrompt(string context, string prompt) {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return string.IsNullOrWhiteSpace(context)
            ? prompt
            : $"""
{context}

User request:
{prompt}
""";
    }

    public static IReadOnlyList<string> NormalizeSelectedNodeIds(IEnumerable<string>? selectedNodeIds) {
        return selectedNodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? [];
    }

}

public static class ContextualAgentAccessResolver {
    public static IReadOnlyList<ContextualAgentAccessSummary> Resolve(
        IEnumerable<AgentDefinition> agents,
        Func<AgentDefinition, ContextualAgentAccessSummary?> resolveAccess) {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(resolveAccess);
        return agents
            .Where(agent => !agent.IsTemplate && agent.Status == AgentLifecycleStatus.Active)
            .Select(resolveAccess)
            .Where(summary => summary is not null)
            .Cast<ContextualAgentAccessSummary>()
            .OrderBy(summary => summary.Agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool ShouldAutoApproveContextualRun(
        IEnumerable<ContextualAgentAccessSummary> accessibleAgents,
        Guid? selectedAgentId,
        bool hasScopedContext) {
        ArgumentNullException.ThrowIfNull(accessibleAgents);
        return selectedAgentId.HasValue && hasScopedContext &&
            accessibleAgents.FirstOrDefault(item => item.Agent.Id == selectedAgentId.Value)?.CanWrite == true;
    }

    public static ContextualAgentAccessLevel ResolveAccessLevel(bool canRead, bool canWrite) {
        return canWrite ? ContextualAgentAccessLevel.Read | ContextualAgentAccessLevel.Write
            : canRead ? ContextualAgentAccessLevel.Read : ContextualAgentAccessLevel.None;
    }
}
