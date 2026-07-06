using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Persistence;

public sealed class MemoryRuntimeService(
    IMemoryOperationHandler operationHandler) : IMemoryRuntimeService
{
    public async Task<MemoryRuntimeOperationResult> ExecuteContextQueryAsync(
        MemoryRuntimeOperationRequest request,
        MemoryContextQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(query);

        var caller = MemoryOperationCaller.RuntimeCompatibility(
            "memory.runtime.context-query",
            request.Requester,
            request.SelectionContext);
        var handlerRequest = MemoryOperationRequestBuilder.Query(
            caller,
            request.SelectionPolicy,
            query,
            request.Retention) with
        {
            CorrelationId = request.CorrelationId,
            CausationId = request.CausationId,
            Extensions = request.Extensions,
            SourceSnapshotIds = request.SourceSnapshotIds.ToArray()
        };
        var result = await operationHandler.ExecuteQueryAsync(handlerRequest, cancellationToken);
        return new MemoryRuntimeOperationResult(
            result.Selection,
            result.OperationRecord,
            result.Output,
            result.AcceptedOperation,
            result.DriverDispatchAttempted,
            result.Diagnostic);
    }
}
