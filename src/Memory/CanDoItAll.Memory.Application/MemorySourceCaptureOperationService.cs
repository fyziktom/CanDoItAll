using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemorySourceCaptureOperationService(
    MemoryOperationCoordinator coordinator,
    IMemorySourceRequestLedgerStore sourceRequestLedgerStore,
    IMemorySourceGateway sourceGateway)
{
    public async Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureAsync(
        MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        MemoryOperationGuard.EnsureOperationKind(request.OperationKind, MemoryOperationKind.Ingestion);
        var selection = await coordinator.SelectProviderAsync(
            request.SelectionPolicy,
            request.Caller.SelectionContext,
            cancellationToken);
        if (!selection.DispatchAllowed || selection.SelectedProvider is null)
        {
            return MemoryOperationResultFactory.Rejected<MemorySourceCaptureOperationResult>(selection);
        }

        if (request.Payload.ProviderInstanceId != selection.SelectedProvider.InstanceId)
        {
            return MemoryOperationResultFactory.Rejected<MemorySourceCaptureOperationResult>(
                MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.ProviderDenied,
                    selection.Reason,
                    request.SelectionPolicy.RequiredCapability,
                    "The source capture provider does not match the selected provider; dispatch is not allowed.",
                    [request.Payload.ProviderInstanceId, selection.SelectedProvider.InstanceId]));
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

        var sourceSnapshotId = CanDoItAll.Memory.Abstractions.MemorySourceSnapshotId.Parse(
            gatewayResult.Snapshot.Manifest.SnapshotId.Value);
        var operationRequest = request with
        {
            SourceSnapshotIds = [sourceSnapshotId]
        };
        var operation = await coordinator.CreateOperationAsync(
            operationRequest,
            selection.SelectedProvider.InstanceId,
            cancellationToken);
        var now = coordinator.UtcNow;
        var jobRecord = new MemorySourceIngestionJobRecord(
            Guid.NewGuid(),
            selection.SelectedProvider.InstanceId,
            request.Payload.SourceGatewayRequest,
            MemorySourceIngestionJobStatus.SnapshotCaptured,
            now,
            now,
            request.Payload.StatusReason,
            gatewayResult.Snapshot.Manifest.SnapshotId,
            operation.OperationId);
        await sourceRequestLedgerStore.EnqueueAsync(jobRecord, cancellationToken);
        var transitioned = await coordinator.OperationLedgerStore.TransitionAsync(
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
