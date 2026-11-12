using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentToolProposalPreparer {
    bool Supports(string toolName);

    AgentToolPreparedPayload Prepare(string toolName, JsonElement arguments);
}

public sealed record AgentToolSessionObservation(AgentToolSessionAdmission Session,
    AgentTurnContextReference? TurnContext, AgentExecutionGovernanceSnapshot? Governance) {
    [JsonIgnore]
    public AgentPlanningDecisionObservation? Planning { get; internal init; }

    internal static AgentToolSessionObservation From(ExecutionRunRecord run)
        => new(run.ToolAdmission!.Session, AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson),
            AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)) {
            Planning = AgentPlanningDecisionObservation.From(run)
        };
}

public sealed class AgentPlanningDecisionObservation {
    private AgentPlanningDecisionObservation(ExecutionRunRecord run) {
        Expected = AgentPlanningDecisionExpectation.From(run);
        Decision = run.ToolAdmission!.PlanningDecision!;
    }

    public AgentPlanningDecisionExpectation Expected { get; }
    public AgentPlanningDecisionRecord Decision { get; }

    internal static AgentPlanningDecisionObservation? From(ExecutionRunRecord run)
        => run.ToolAdmission?.PlanningDecision is null ? null : new(run);
}

public interface IAgentToolAdmissionVerifier {
    ValueTask<AgentToolSessionAdmission> RequireSessionAsync(
        AgentToolSessionReference reference,
        CancellationToken cancellationToken = default);

    ValueTask<AgentToolSessionObservation> RequireSessionObservationAsync(
        AgentToolSessionReference reference,
        CancellationToken cancellationToken = default)
        => throw new AgentToolAdmissionException("tool-admission.session-observation-unavailable",
            "The active admission owner does not provide a verified saved-session observation.");

    ValueTask<AgentToolPriorInvocationObservation> RequirePriorInvocationsAsync(
        AgentToolSessionReference session,
        CancellationToken cancellationToken = default)
        => throw new AgentToolAdmissionException("tool-admission.prior-invocations-unavailable",
            "The active admission owner does not provide verified prior invocation evidence.");

    ValueTask<AgentToolPriorReceiptObservation> RequirePriorResultInvocationsAsync(
        AgentToolSessionReference session, AgentToolBusinessIntentId intent, AgentToolPreparedPayload payload,
        CancellationToken cancellationToken = default)
        => throw new AgentToolAdmissionException("tool-admission.prior-results-unavailable",
            "The active admission owner does not provide verified prior result evidence.");

    // Resolve the current serial dispatch without allocating an intent from caller arguments.
    ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(
        AgentToolSessionReference session,
        string toolName,
        AgentToolSemanticDigest digest,
        CancellationToken cancellationToken = default);
}
