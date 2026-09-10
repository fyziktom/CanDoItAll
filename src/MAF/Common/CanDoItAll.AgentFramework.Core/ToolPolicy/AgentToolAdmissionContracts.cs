using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentToolProposalPreparer {
    bool Supports(string toolName);

    AgentToolPreparedPayload Prepare(string toolName, JsonElement arguments);
}

public interface IAgentToolAdmissionVerifier {
    ValueTask<AgentToolSessionAdmission> RequireSessionAsync(
        AgentToolSessionReference reference,
        CancellationToken cancellationToken = default);

    // Resolve the current serial dispatch from the persisted batch and its active execution lease.
    // The caller's tool arguments and diagnostic protocol call id cannot allocate an intent.
    ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(
        AgentToolSessionReference session,
        string toolName,
        AgentToolSemanticDigest digest,
        CancellationToken cancellationToken = default);
}
