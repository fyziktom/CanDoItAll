using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Tooling;

public sealed record AgentRuntimeSavedEffectContext {
    public AgentRuntimeSavedEffectContext(AgentToolSessionAdmission session, AgentDefinition agent,
        AgentExecutionGovernanceSnapshot governance) {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(governance);
        if (session.Reference.BackgroundSource is not null || session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
            session.AgentId != agent.Id || governance.AgentId != agent.Id ||
            governance.AuthorityId != session.Reference.AuthorityId || governance.DatabaseProfileId != session.Profile.ProfileId ||
            governance.DatabaseProfileGeneration != session.Profile.Generation || !governance.ReadAllowed || !governance.MutationAllowed) {
            throw new ArgumentException("Saved-effect restoration requires the exact admitted interactive authority and selected Agent.");
        }
        Session = session;
        Agent = agent with {
            Permissions = agent.Permissions with { AllowedSecrets = agent.Permissions.NormalizedAllowedSecrets.ToImmutableArray() },
            Capabilities = agent.Capabilities.ToImmutableArray(),
            Tags = agent.Tags.ToImmutableArray()
        };
        Governance = governance;
    }

    public AgentToolSessionAdmission Session { get; }
    public AgentDefinition Agent { get; }
    public AgentExecutionGovernanceSnapshot Governance { get; }
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
