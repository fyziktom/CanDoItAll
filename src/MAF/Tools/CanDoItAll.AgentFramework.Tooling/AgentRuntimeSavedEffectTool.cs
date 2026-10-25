using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Tooling;

public sealed record AgentRuntimeSavedEffectContext {
    public AgentRuntimeSavedEffectContext(AgentToolSessionAdmission session, AgentDefinition agent,
        AgentExecutionGovernanceSnapshot governance)
        : this(session, agent, governance?.WorkspaceScope ?? throw new ArgumentNullException(nameof(governance)), governance, null) { }

    public static AgentRuntimeSavedEffectContext ForInherited(AgentToolSessionAdmission session, AgentDefinition agent,
        AgentPlanningInheritedOrigin original, WorkspaceScopeDescriptor contextScope) {
        ArgumentNullException.ThrowIfNull(original);
        _ = original.ResolvePolicy();
        return new(session, agent, contextScope, null, original);
    }

    private AgentRuntimeSavedEffectContext(AgentToolSessionAdmission session, AgentDefinition agent,
        WorkspaceScopeDescriptor contextScope, AgentExecutionGovernanceSnapshot? governance, AgentPlanningInheritedOrigin? original) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(contextScope);
        if (session.AgentId != agent.Id || !Enum.IsDefined(contextScope.Kind) ||
            contextScope.Kind != WorkspaceScopeKind.Sandbox && string.IsNullOrWhiteSpace(contextScope.Key) ||
            contextScope != new WorkspaceScopeDescriptor(contextScope.Kind, contextScope.Key)) {
            throw new ArgumentException("Saved-effect restoration requires its admitted Agent and canonical context scope.");
        }
        if (original is not null) {
            if (governance is not null || session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                session.Reference.BackgroundSource != original.Source || session.Profile != original.Profile ||
                original.AgentId != agent.Id || session.Reference.AuthorityId.Value != Guid.Empty ||
                session.Reference.ChatSessionId != Guid.Empty) {
                throw new ArgumentException("Inherited restoration requires its exact original parent source, profile and executor.");
            }
        } else if (session.Reference.BackgroundSource is not null || session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
            governance is null || governance.AgentId != agent.Id || governance.AuthorityId != session.Reference.AuthorityId ||
            governance.DatabaseProfileId != session.Profile.ProfileId || governance.DatabaseProfileGeneration != session.Profile.Generation ||
            !governance.ReadAllowed || !governance.MutationAllowed) {
            throw new ArgumentException("Interactive restoration requires the exact admitted execution authority.");
        }
        Session = session;
        Agent = agent with {
            Permissions = agent.Permissions with { AllowedSecrets = agent.Permissions.NormalizedAllowedSecrets.ToImmutableArray() },
            Capabilities = agent.Capabilities.ToImmutableArray(),
            Tags = agent.Tags.ToImmutableArray()
        };
        Governance = governance;
        InheritedOrigin = original;
        ContextScope = contextScope;
    }

    public AgentToolSessionAdmission Session { get; }
    public AgentDefinition Agent { get; }
    public AgentExecutionGovernanceSnapshot? Governance { get; }
    public AgentPlanningInheritedOrigin? InheritedOrigin { get; }
    public WorkspaceScopeDescriptor ContextScope { get; }
}

public sealed record AgentRuntimeSavedEffectTool {
    public AgentRuntimeSavedEffectTool(AIFunction function, AgentRuntimeToolMetadata metadata) {
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!string.Equals(function.Name, metadata.ToolName, StringComparison.Ordinal) || metadata.Unavailability is not null ||
            metadata.RestoreAdmission is null || metadata.AuthorizeAdmissionAsync is null || metadata.AuthorizeResultDisclosureAsync is null) {
            throw new ArgumentException("A saved-effect function requires exact metadata, explicit restoration and current invocation/result authorization.");
        }
        Function = function;
        Metadata = metadata;
    }

    public AIFunction Function { get; }
    public AgentRuntimeToolMetadata Metadata { get; }
}
