using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentPlanningRunPreparation(AgentDefinition Agent, CatalogDataRevision CatalogRevision,
    AgentToolProfileBinding Profile, AgentTurnContextReference Source, AgentExecutionGovernanceSnapshot Governance);

public sealed class AgentPlanningRunAdmission {
    private readonly AgentToolProfileBinding profile;
    private readonly AgentExecutionAuthorityId authority;
    private readonly Func<AgentToolSessionAdmission, AgentToolProtocolEnvelope> bind;

    public AgentPlanningRunAdmission(AgentToolProfileBinding profile, AgentExecutionAuthorityId authority,
        AgentPlanningPolicyProvenance provenance, Func<AgentToolSessionAdmission, AgentToolProtocolEnvelope> bind) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(bind);
        if (authority.IsEmpty) {
            throw new ArgumentException("A planning admission requires its original execution authority.", nameof(authority));
        }
        _ = provenance.Resolve();
        this.profile = profile;
        this.authority = authority;
        this.bind = bind;
        Provenance = provenance;
    }

    public AgentPlanningPolicyProvenance Provenance { get; }

    internal AgentToolProtocolEnvelope BindOriginalTarget(AgentToolSessionAdmission session) {
        if (session.AgentId != Provenance.AgentId || session.Profile != profile || session.Reference.AuthorityId != authority ||
            session.Purpose != AgentRuntimeContextPurpose.InteractiveChat || session.Reference.BackgroundSource is not null) {
            throw new AgentToolAdmissionException("planning.admission-changed", "The new run does not match its prepared planning authority.");
        }
        return bind(session) ?? throw new InvalidOperationException("The planning participant returned no original target.");
    }
}
