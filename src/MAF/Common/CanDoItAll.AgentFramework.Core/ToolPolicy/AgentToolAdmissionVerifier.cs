using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentToolAdmissionVerifier : IAgentToolAdmissionVerifier {
    public ValueTask<AgentToolSessionAdmission> RequireSessionAsync(AgentToolSessionReference session,
        CancellationToken cancellationToken = default)
        => RequireOwner().RequireSessionAsync(session, cancellationToken);

    public ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(AgentToolSessionReference session,
        string toolName, AgentToolSemanticDigest digest, CancellationToken cancellationToken = default)
        => RequireOwner().RequireInvocationAsync(session, toolName, digest, cancellationToken);

    private static IAgentToolAdmissionVerifier RequireOwner()
        => AgentToolRunLease.Current?.AdmissionVerifier ?? throw new AgentToolAdmissionException(
            "tool-admission.no-active-lease", "A live canonical run dispatch lease is required for tool authorization.");
}
