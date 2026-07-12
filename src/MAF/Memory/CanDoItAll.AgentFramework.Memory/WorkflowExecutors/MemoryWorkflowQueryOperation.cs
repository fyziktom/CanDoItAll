using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory;

internal sealed class MemoryWorkflowQueryOperation(
    IMemoryOperationHandler operationHandler,
    MemoryWorkflowRequestFactory requests)
{
    public async Task<object> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        MemoryWorkflowExecutorSettings settings,
        CancellationToken cancellationToken)
    {
        var queryText = MemoryWorkflowInputResolver.ResolveQueryText(settings, input);
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return MemoryMafToolResultShaper.RejectedQuery(
                MemoryToolResultStatus.InvalidRequest,
                "Memory workflow context query requires a non-empty query.");
        }

        var capability = settings.AllowAsync
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        var policy = requests.ResolvePolicy(
            context,
            settings,
            capability,
            settings.ProviderInstanceId,
            providerRequired: false);
        if (policy.Rejection is { } rejection)
        {
            return MemoryMafToolResultShaper.RejectedQuery(rejection.Status, rejection.Diagnostic);
        }

        var sourceSnapshotId = MemoryWorkflowInputResolver.TryParseFirstSourceSnapshotId(
            settings.SourceSnapshotIds);
        var query = new MemoryContextQueryRequest(
            queryText,
            [capability],
            sourceSnapshotId is null
                ? MemorySourceProvenance.None
                : new MemorySourceProvenance(
                    sourceSnapshotId,
                    SourceModule: null,
                    SourceRecordIds: [],
                    Citations: []));
        var request = MemoryOperationRequestBuilder.Query(
            MemoryWorkflowRequestFactory.CreateWorkflowCaller(context),
            policy.SelectionPolicy,
            query,
            requests.CreateRetention());
        var result = await operationHandler.ExecuteQueryAsync(request, cancellationToken).ConfigureAwait(false);
        return MemoryMafToolResultShaper.ToQueryResult(result);
    }
}
