using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Pure mapping from runtime build outputs (provider responses, snapshots, usage
/// observations, and context diagnostics) into the transport-neutral
/// <see cref="AgentRuntimeResponse"/> shape. It performs no I/O, holds no state,
/// and is directly unit-testable.
/// </summary>
internal static class MafRuntimeResponseMapper
{
    /// <summary>
    /// Attaches the context assembly manifest, context contribution traces, and entry-agent
    /// request compatibility evidence to a runtime response without changing any other field.
    /// </summary>
    public static AgentRuntimeResponse AttachContextDiagnostics(
        AgentRuntimeResponse response,
        AgentRuntimeContextAssemblyManifest contextManifest,
        IReadOnlyList<AgentContextContributionTrace> contextContributionTraces,
        ProviderRequestCompatibilityEvidence? entryAgentRequestCompatibilityEvidence)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response with
        {
            ContextAssemblyManifest = contextManifest,
            ContextContributionTraces = contextContributionTraces,
            EntryAgentRequestCompatibilityEvidence = entryAgentRequestCompatibilityEvidence
        };
    }

    /// <summary>
    /// Maps a terminal provider completion into the runtime response contract: token usage,
    /// tool call counts, finalizer invocations, tool invocation traces, usage observations,
    /// pending approvals, and session key/state.
    /// </summary>
    public static AgentRuntimeResponse CreateTerminalRuntimeResponse(
        string responseText,
        AgentResponse response,
        AgentResponse activityResponse,
        string runtimeSessionKey,
        string? serializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord> pendingApprovals,
        IReadOnlyList<AgentFinalizerInvocation> finalizerInvocations,
        IReadOnlyList<AgentToolInvocationTrace> toolInvocationTraces,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(activityResponse);
        ArgumentNullException.ThrowIfNull(pendingApprovals);

        return new AgentRuntimeResponse(
            ResponseText: responseText,
            InputTokens: MafRuntimeResponseAssembler.ClampTokenCount(response.Usage?.InputTokenCount),
            OutputTokens: MafRuntimeResponseAssembler.ClampTokenCount(response.Usage?.OutputTokenCount),
            ToolCalls: MafRuntimeResponseAssembler.CountToolCalls(activityResponse),
            RuntimeSessionKey: runtimeSessionKey,
            SerializedSessionStateJson: serializedSessionStateJson,
            PendingApprovals: pendingApprovals)
        {
            CachedInputTokens = MafRuntimeResponseAssembler.ClampTokenCount(response.Usage?.CachedInputTokenCount),
            FinalizerInvocations = finalizerInvocations,
            ToolInvocationTraces = toolInvocationTraces,
            UsageObservations = usageObservations
        };
    }

    /// <summary>
    /// Appends usage observations captured while preparing request-scoped input attachments
    /// to the observations already carried by the runtime response.
    /// </summary>
    public static AgentRuntimeResponse AttachPreparedInputUsageObservations(
        AgentRuntimeResponse response,
        IReadOnlyList<ProviderUsageObservation>? usageObservations)
    {
        return InputAttachmentPreparer.AttachUsageObservations(response, usageObservations);
    }
}
