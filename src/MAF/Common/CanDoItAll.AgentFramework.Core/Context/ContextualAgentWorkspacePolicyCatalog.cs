using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record ContextualAgentWorkspacePolicyDescriptor(
    ContextualAgentWorkspaceKind WorkspaceKind,
    string WorkspaceLabel,
    string SelectedScopeLabel,
    string EmptyAccessDescription,
    AgentChatContextSourceKind SourceKind,
    bool IncludesSelectedNodeMetadata);

public interface IContextualAgentWorkspacePolicy {
    ContextualAgentWorkspacePolicyDescriptor Descriptor { get; }

    Guid? ResolveScopeId(Guid? projectId, Guid? processDefinitionId);

    ContextualAgentAccessSummary? ResolveAccess(AgentDefinition agent, Guid? scopeId);

    string BuildContext(Guid? scopeId, IEnumerable<string>? selectedNodeIds);
}

public sealed class ContextualAgentWorkspacePolicyCatalog {
    private readonly FrozenDictionary<ContextualAgentWorkspaceKind, IContextualAgentWorkspacePolicy> policies;

    public ContextualAgentWorkspacePolicyCatalog(IEnumerable<IContextualAgentWorkspacePolicy> policies) {
        ArgumentNullException.ThrowIfNull(policies);
        var registered = new Dictionary<ContextualAgentWorkspaceKind, IContextualAgentWorkspacePolicy>();
        foreach (var policy in policies) {
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(policy.Descriptor);
            if (!Enum.IsDefined(policy.Descriptor.WorkspaceKind)) {
                throw new ArgumentException("A contextual workspace policy has an undefined compatibility kind.", nameof(policies));
            }
            if (!registered.TryAdd(policy.Descriptor.WorkspaceKind, policy)) {
                throw new ArgumentException($"Contextual workspace '{policy.Descriptor.WorkspaceKind}' has more than one policy owner.", nameof(policies));
            }
        }
        this.policies = registered.ToFrozenDictionary();
    }

    public IContextualAgentWorkspacePolicy Require(ContextualAgentWorkspaceKind workspaceKind) {
        return policies.TryGetValue(workspaceKind, out var policy) ? policy
            : throw new InvalidOperationException($"Contextual workspace '{workspaceKind}' has no registered policy owner.");
    }

    public IReadOnlyList<ContextualAgentAccessSummary> Resolve(
        IEnumerable<AgentDefinition> agents,
        ContextualAgentWorkspaceKind workspaceKind,
        Guid? projectId = null,
        Guid? processDefinitionId = null) {
        var policy = Require(workspaceKind);
        var scopeId = policy.ResolveScopeId(projectId, processDefinitionId);
        return ContextualAgentAccessResolver.Resolve(agents, agent => policy.ResolveAccess(agent, scopeId));
    }

    public string BuildPrompt(
        ContextualAgentWorkspaceKind workspaceKind,
        Guid? projectId,
        Guid? processDefinitionId,
        IEnumerable<string>? selectedNodeIds,
        string prompt) {
        var policy = Require(workspaceKind);
        return ContextualAgentWorkspaceContextBuilder.BuildPrompt(
            policy.BuildContext(policy.ResolveScopeId(projectId, processDefinitionId), selectedNodeIds), prompt);
    }

    public bool ShouldAutoApproveContextualRun(
        IEnumerable<ContextualAgentAccessSummary> accessibleAgents,
        ContextualAgentWorkspaceKind workspaceKind,
        Guid? selectedAgentId,
        Guid? projectId = null,
        Guid? processDefinitionId = null) {
        var policy = Require(workspaceKind);
        return ContextualAgentAccessResolver.ShouldAutoApproveContextualRun(
            accessibleAgents, selectedAgentId, policy.ResolveScopeId(projectId, processDefinitionId).HasValue);
    }
}
