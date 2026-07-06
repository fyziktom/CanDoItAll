using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed partial class MemoryOperationHandler
{
    public async Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
        MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOperationKind(request.OperationKind, MemoryOperationKind.Ingestion);
        var selection = await SelectProviderAsync(request.SelectionPolicy, request.Caller.SelectionContext, cancellationToken);
        if (!selection.DispatchAllowed || selection.SelectedProvider is null)
        {
            return Rejected<MemorySourceCaptureOperationResult>(selection);
        }

        var gatewayResult = await sourceGateway.ReadSnapshotAsync(
            request.Payload.SourceGatewayRequest,
            cancellationToken);
        if (gatewayResult.Status != MemorySourceGatewayStatus.Succeeded || gatewayResult.Snapshot is null)
        {
            return new MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>(
                MemoryOperationHandlerStatus.SourceCaptureFailed,
                selection,
                OperationRecord: null,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                $"Source capture failed with status '{gatewayResult.Status}': {gatewayResult.Diagnostic}");
        }

        var sourceSnapshotId = MemorySourceSnapshotId.Parse(gatewayResult.Snapshot.Manifest.SnapshotId.Value);
        var operationRequest = request with
        {
            SourceSnapshotIds = [sourceSnapshotId]
        };
        var operation = await CreateOperationAsync(
            operationRequest,
            selection.SelectedProvider.InstanceId,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var jobRecord = new MemorySourceIngestionJobRecord(
            Guid.NewGuid(),
            request.Payload.ProviderInstanceId,
            request.Payload.SourceGatewayRequest,
            MemorySourceIngestionJobStatus.SnapshotCaptured,
            now,
            now,
            request.Payload.StatusReason,
            gatewayResult.Snapshot.Manifest.SnapshotId,
            operation.OperationId);
        await sourceRequestLedgerStore.EnqueueAsync(jobRecord, cancellationToken);
        var transitioned = await operationLedgerStore.TransitionAsync(
            operation.OperationId,
            MemoryLedgerStatus.Accepted,
            now,
            "Source snapshot captured and queued for provider ingestion.",
            operation.Extensions,
            cancellationToken);

        return new MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>(
            MemoryOperationHandlerStatus.Accepted,
            selection,
            transitioned,
            new MemorySourceCaptureOperationResult(jobRecord, gatewayResult.PayloadForms),
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            "Source snapshot captured and queued for provider ingestion.");
    }
}
