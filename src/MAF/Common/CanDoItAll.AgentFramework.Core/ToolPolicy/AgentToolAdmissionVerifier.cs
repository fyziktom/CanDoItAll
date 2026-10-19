using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentToolAdmissionVerifier : IAgentToolAdmissionVerifier {
    public ValueTask<AgentToolSessionAdmission> RequireSessionAsync(AgentToolSessionReference session,
        CancellationToken cancellationToken = default)
        => RequireOwner().RequireSessionAsync(session, cancellationToken);

    public ValueTask<AgentToolSessionObservation> RequireSessionObservationAsync(AgentToolSessionReference session,
        CancellationToken cancellationToken = default)
        => RequireOwner().RequireSessionObservationAsync(session, cancellationToken);

    public ValueTask<AgentToolPriorInvocationObservation> RequirePriorInvocationsAsync(AgentToolSessionReference session,
        CancellationToken cancellationToken = default)
        => RequireOwner().RequirePriorInvocationsAsync(session, cancellationToken);

    public ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(AgentToolSessionReference session,
        string toolName, AgentToolSemanticDigest digest, CancellationToken cancellationToken = default)
        => RequireOwner().RequireInvocationAsync(session, toolName, digest, cancellationToken);

    public ValueTask<AgentToolPriorReceiptObservation> RequirePriorResultInvocationsAsync(AgentToolSessionReference session,
        AgentToolBusinessIntentId intent, AgentToolPreparedPayload payload, CancellationToken cancellationToken = default)
        => RequireOwner().RequirePriorResultInvocationsAsync(session, intent, payload, cancellationToken);

    private static IAgentToolAdmissionVerifier RequireOwner()
        => AgentToolRunLease.Current?.AdmissionVerifier ?? throw new AgentToolAdmissionException(
            "tool-admission.no-active-lease", "A live canonical run dispatch lease is required for tool authorization.");
}
