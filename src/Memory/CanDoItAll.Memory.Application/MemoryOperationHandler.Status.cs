using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed partial class MemoryOperationHandler
{
    public async Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
        MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOperationKind(request.OperationKind, MemoryOperationKind.OperationStatus);
        var operation = await operationLedgerStore.GetAsync(request.Payload.OperationId, cancellationToken);
        if (operation is null)
        {
            return NotFound<MemoryOperationRecord>(
                request.SelectionPolicy,
                $"Memory operation '{request.Payload.OperationId}' was not found.");
        }

        var selection = await SelectProviderAsync(
            request.SelectionPolicy with
            {
                ExplicitProviderId = operation.ProviderInstanceId
            },
            request.Caller.SelectionContext,
            cancellationToken);
        return new MemoryOperationHandlerResult<MemoryOperationRecord>(
            MemoryOperationHandlerStatus.Completed,
            selection,
            operation,
            operation,
            AcceptedOperation: null,
            FeedbackHandle: operation.Extensions.GetContextDelivery()?.FeedbackHandle,
            DriverDispatchAttempted: false,
            "Memory operation status returned.");
    }

    public async Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
        MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOperationKind(request.OperationKind, MemoryOperationKind.OperationStatus);
        var existing = await operationLedgerStore.GetAsync(request.Payload.OperationId, cancellationToken);
        if (existing is null)
        {
            return NotFound<MemoryOperationRecord>(
                request.SelectionPolicy,
                $"Memory operation '{request.Payload.OperationId}' was not found.");
        }

        var selection = await SelectProviderAsync(
            request.SelectionPolicy with
            {
                ExplicitProviderId = existing.ProviderInstanceId
            },
            request.Caller.SelectionContext,
            cancellationToken);
        if (!selection.DispatchAllowed)
        {
            return Rejected<MemoryOperationRecord>(selection);
        }

        var cancelled = await operationLedgerStore.TransitionAsync(
            existing.OperationId,
            MemoryLedgerStatus.Cancelled,
            timeProvider.GetUtcNow(),
            NormalizeReason(request.Payload.Reason),
            existing.Extensions.WithMemoryOperationCaller(request.Caller),
            cancellationToken);
        return new MemoryOperationHandlerResult<MemoryOperationRecord>(
            MemoryOperationHandlerStatus.Cancelled,
            selection,
            cancelled,
            cancelled,
            AcceptedOperation: null,
            FeedbackHandle: cancelled.Extensions.GetContextDelivery()?.FeedbackHandle,
            DriverDispatchAttempted: false,
            "Memory operation cancelled.");
    }
}
