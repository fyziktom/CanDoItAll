using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentToolProposalPreparer {
    bool Supports(string toolName);

    AgentToolPreparedPayload Prepare(string toolName, JsonElement arguments);
}

public sealed record AgentToolSessionObservation(AgentToolSessionAdmission Session,
    AgentTurnContextReference? TurnContext, AgentExecutionGovernanceSnapshot? Governance);

public interface IAgentToolAdmissionVerifier {
    ValueTask<AgentToolSessionAdmission> RequireSessionAsync(
        AgentToolSessionReference reference,
        CancellationToken cancellationToken = default);

    ValueTask<AgentToolSessionObservation> RequireSessionObservationAsync(
        AgentToolSessionReference reference,
        CancellationToken cancellationToken = default)
        => throw new AgentToolAdmissionException("tool-admission.session-observation-unavailable",
            "The active admission owner does not provide a verified saved-session observation.");

    // Resolve the current serial dispatch from the persisted batch and its active execution lease.
    // The caller's tool arguments and diagnostic protocol call id cannot allocate an intent.
    ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(
        AgentToolSessionReference session,
        string toolName,
        AgentToolSemanticDigest digest,
        CancellationToken cancellationToken = default);
}
