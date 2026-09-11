using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes.AgentChat;

public sealed class ProcessContextualWorkspacePolicy : IContextualAgentWorkspacePolicy {
    public ContextualAgentWorkspacePolicyDescriptor Descriptor { get; } = new(
        ContextualAgentWorkspaceKind.Processes, "Processes", "This process",
        "Create or edit an agent with process access for this definition.",
        new("process-definition"), IncludesSelectedNodeMetadata: false);

    public Guid? ResolveScopeId(Guid? projectId, Guid? processDefinitionId) => processDefinitionId;

    public string BuildContext(Guid? scopeId, IEnumerable<string>? selectedNodeIds) {
        return scopeId.HasValue ? $"""
Context:
- Workspace: process definition.
- Selected process definition id: {scopeId.Value:D}.
- Treat "this process" and "selected process" as that process definition.
- Use process-definition operations for process reads or mutations.
- For adding one process role, use {ProcessCompatibilityToolPolicy.ProcessesDefinitionRoleAdd} instead of loading and rewriting the full editor model.
- Do not use project-structure operations unless the user explicitly asks about project structure.
""" : string.Empty;
    }

    public ContextualAgentAccessSummary? ResolveAccess(
        AgentDefinition agent,
        Guid? scopeId) {
        var access = AgentProcessAccessMetadata.Read(agent.ConfigurationJson);
        var accessLevel = ContextualAgentAccessResolver.ResolveAccessLevel(access.CanRead, access.CanWrite);
        if (accessLevel == ContextualAgentAccessLevel.None) {
            return null;
        }

        if (!access.AllowAllDefinitions) {
            if (scopeId.HasValue && !access.AllowedDefinitionIds.Contains(scopeId.Value)) {
                return null;
            }

            if (!scopeId.HasValue && access.AllowedDefinitionIds.Count == 0) {
                return null;
            }
        }

        var scopeLabel = access.AllowAllDefinitions
            ? "All processes"
            : scopeId.HasValue
                ? "This process"
                : access.AllowedDefinitionIds.Count == 1 ? "1 process" : $"{access.AllowedDefinitionIds.Count} processes";

        return new ContextualAgentAccessSummary(agent, accessLevel, scopeLabel);
    }

}
