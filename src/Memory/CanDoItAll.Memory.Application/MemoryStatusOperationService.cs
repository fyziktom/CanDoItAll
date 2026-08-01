using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryStatusOperationService(
    MemoryOperationCoordinator coordinator,
    IMemoryOperationAccessAuthorizer accessAuthorizer)
{
    public async Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
        MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        MemoryOperationGuard.EnsureOperationKind(request.OperationKind, MemoryOperationKind.OperationStatus);
        var operation = await coordinator.OperationLedgerStore.GetAsync(
            request.Payload.OperationId,
            cancellationToken);
        if (operation is null)
        {
            return MemoryOperationResultFactory.NotFound<MemoryOperationRecord>(
                request.SelectionPolicy,
                $"Memory operation '{request.Payload.OperationId}' was not found.");
        }

        if (!accessAuthorizer.Authorize(operation.Requester, request.Caller.Requester).IsAllowed)
        {
            return MemoryOperationResultFactory.AccessDenied<MemoryOperationRecord>(request.SelectionPolicy);
        }

        var selection = await SelectOperationProviderAsync(
            request.SelectionPolicy,
            operation.ProviderInstanceId,
            request.Caller.SelectionContext,
            cancellationToken);
        if (!selection.DispatchAllowed)
        {
            return MemoryOperationResultFactory.Rejected<MemoryOperationRecord>(selection);
        }

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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        MemoryOperationGuard.EnsureOperationKind(request.OperationKind, MemoryOperationKind.OperationStatus);
        var existing = await coordinator.OperationLedgerStore.GetAsync(
            request.Payload.OperationId,
            cancellationToken);
        if (existing is null)
        {
            return MemoryOperationResultFactory.NotFound<MemoryOperationRecord>(
                request.SelectionPolicy,
                $"Memory operation '{request.Payload.OperationId}' was not found.");
        }

        if (!accessAuthorizer.Authorize(existing.Requester, request.Caller.Requester).IsAllowed)
        {
            return MemoryOperationResultFactory.AccessDenied<MemoryOperationRecord>(request.SelectionPolicy);
        }

        var selection = await SelectOperationProviderAsync(
            request.SelectionPolicy,
            existing.ProviderInstanceId,
            request.Caller.SelectionContext,
            cancellationToken);
        if (!selection.DispatchAllowed)
        {
            return MemoryOperationResultFactory.Rejected<MemoryOperationRecord>(selection);
        }

        var cancelled = await coordinator.OperationLedgerStore.TransitionAsync(
            existing.OperationId,
            MemoryLedgerStatus.Cancelled,
            coordinator.UtcNow,
            MemoryOperationGuard.NormalizeReason(request.Payload.Reason),
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
            "Memory operation tracking was cancelled locally; provider cancellation was not dispatched.");
    }

    private Task<MemoryProviderSelectionResult> SelectOperationProviderAsync(
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryProviderInstanceId providerInstanceId,
        MemoryProviderSelectionContext selectionContext,
        CancellationToken cancellationToken)
    {
        return coordinator.SelectProviderAsync(
            selectionPolicy with
            {
                ExplicitProviderId = providerInstanceId
            },
            selectionContext,
            cancellationToken);
    }
}
