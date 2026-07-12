using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory.Tools;

internal sealed class MemoryAgentQueryTools(
    IMemoryOperationHandler operationHandler,
    TimeProvider timeProvider)
{
    public async Task<MemoryContextQueryToolResult> QueryAsync(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryContextQueryToolInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.Query))
        {
            return MemoryMafToolResultShaper.RejectedQuery(
                MemoryToolResultStatus.InvalidRequest,
                "Memory context query requires a non-empty query.");
        }

        var capability = input.AllowAsync
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        var policy = MemoryAgentToolPolicyFactory.Resolve(
            context,
            access,
            capability,
            input.ProviderInstanceId,
            providerRequired: true);
        if (policy.Resolution.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedQuery(rejection.Status, rejection.Diagnostic);
        }

        var sourceSnapshotId = TryParseFirstSourceSnapshotId(input.SourceSnapshotIds);
        var payload = new MemoryContextQueryRequest(
            input.Query.Trim(),
            [capability],
            sourceSnapshotId is null
                ? MemorySourceProvenance.None
                : new MemorySourceProvenance(sourceSnapshotId, SourceModule: null, SourceRecordIds: [], Citations: []))
        {
            Context = policy.RequestContext
        };
        var request = MemoryOperationRequestBuilder.Query(
            MemoryAgentToolPolicyFactory.CreateCaller(policy, MemoryAgentRuntimeToolNames.ContextQuery),
            policy.Resolution.SelectionPolicy,
            payload,
            MemoryMafRetentionPolicyFactory.Create(timeProvider));
        var result = await operationHandler.ExecuteQueryAsync(request, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToQueryResult(result);
    }

    private static MemorySourceSnapshotId? TryParseFirstSourceSnapshotId(
        IReadOnlyList<string>? sourceSnapshotIds)
    {
        var value = sourceSnapshotIds?.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        return string.IsNullOrWhiteSpace(value)
            ? null
            : MemorySourceSnapshotId.Parse(value.Trim());
    }
}
