// Runtime response/failure contracts moved verbatim from
// CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs. The namespace now matches the owning
// project (CanDoItAll.AgentFramework.Runtime.Abstractions); the trace payload models referenced
// below still live in the Models project. SB18 deleted the broad IAgentRuntime interface that used to live in this file
// (production/tests now consume the four narrow runtime ports exclusively) — the types below are
// still the canonical response/failure shapes the ports return and are not legacy.
using CanDoItAll.AgentFramework.Models;

using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

public sealed record AgentRuntimeResponse(
    string ResponseText,
    int InputTokens,
    int OutputTokens,
    int ToolCalls,
    string RuntimeSessionKey,
    string? SerializedSessionStateJson,
    IReadOnlyList<PendingToolApprovalRecord> PendingApprovals)
{
    public int CachedInputTokens { get; init; }

    public IReadOnlyList<AgentFinalizerInvocation> FinalizerInvocations { get; init; } = [];

    public IReadOnlyList<AgentToolInvocationTrace> ToolInvocationTraces { get; init; } = [];

    public IReadOnlyList<ProviderUsageObservation> UsageObservations { get; init; } = [];

    public AgentRuntimeContextAssemblyManifest? ContextAssemblyManifest { get; init; }

    public IReadOnlyList<AgentContextContributionTrace> ContextContributionTraces { get; init; } = [];

    public ProviderRequestCompatibilityEvidence? EntryAgentRequestCompatibilityEvidence { get; init; }
}

public enum AgentRuntimeFailureOrigin
{
    Runtime,
    Provider,
    Tool,
    Finalizer,
    ProviderConfiguration
}

public sealed record AgentRuntimeProviderFailureIdentity(
    Guid ProviderProfileId,
    string ProviderName,
    ProviderKind ProviderKind,
    ProviderTransportKind Transport,
    string Model);

public sealed class AgentRuntimeUsageException : Exception
{
    public AgentRuntimeUsageException(
        string message,
        Exception innerException,
        IReadOnlyList<ProviderUsageObservation> usageObservations,
        IReadOnlyList<AgentToolInvocationTrace>? toolInvocationTraces = null,
        ProviderRequestCompatibilityEvidence? entryAgentRequestCompatibilityEvidence = null,
        AgentRuntimeFailureOrigin failureOrigin = AgentRuntimeFailureOrigin.Runtime,
        AgentRuntimeProviderFailureIdentity? providerFailureIdentity = null)
        : base(message, innerException)
    {
        UsageObservations = usageObservations;
        ToolInvocationTraces = toolInvocationTraces ?? [];
        EntryAgentRequestCompatibilityEvidence = entryAgentRequestCompatibilityEvidence;
        FailureOrigin = failureOrigin;
        ProviderFailureIdentity = providerFailureIdentity;
    }

    public IReadOnlyList<ProviderUsageObservation> UsageObservations { get; }

    public IReadOnlyList<AgentToolInvocationTrace> ToolInvocationTraces { get; }

    public ProviderRequestCompatibilityEvidence? EntryAgentRequestCompatibilityEvidence { get; }

    public AgentRuntimeFailureOrigin FailureOrigin { get; }

    public AgentRuntimeProviderFailureIdentity? ProviderFailureIdentity { get; }
}
