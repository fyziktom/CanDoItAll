using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryFeedbackOperationService(
    MemoryOperationCoordinator coordinator,
    IMemoryFeedbackLedgerStore feedbackLedgerStore,
    IEnumerable<IMemoryProviderFeedbackDeliveryDriver> feedbackDrivers)
{
    private readonly MemoryProviderDriverCatalog<IMemoryProviderFeedbackDeliveryDriver> driverCatalog =
        new(feedbackDrivers, static driver => driver.DriverKind);

    public async Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitAsync(
        MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        MemoryOperationGuard.EnsureOperationKind(request.OperationKind, MemoryOperationKind.Feedback);
        var selection = await coordinator.SelectProviderAsync(
            request.SelectionPolicy,
            request.Caller.SelectionContext,
            cancellationToken);
        if (!selection.DispatchAllowed || selection.SelectedProvider is null)
        {
            return MemoryOperationResultFactory.Rejected<MemoryFeedbackRecord>(selection);
        }

        var driver = driverCatalog.ResolveUnique(
            selection.SelectedProvider.DriverKind,
            out var driverFailure);
        if (driver is null)
        {
            return MemoryOperationResultFactory.DriverUnavailable<MemoryFeedbackRecord>(
                selection,
                driverFailure);
        }

        var feedback = MemoryFeedbackRecord.CreateUnmatched(
            MemoryFeedbackRecordId.New(),
            selection.SelectedProvider.InstanceId,
            request.Payload.Stage,
            request.Payload.Feedback.Outcome,
            request.Caller.Requester,
            request.Payload.UnmatchedReason,
            request.Retention,
            coordinator.UtcNow,
            request.Payload.Feedback.EconomicImpact);
        await feedbackLedgerStore.SubmitAsync(feedback, cancellationToken);
        return new MemoryOperationHandlerResult<MemoryFeedbackRecord>(
            MemoryOperationHandlerStatus.Accepted,
            selection,
            OperationRecord: null,
            feedback,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            "Memory feedback accepted for delivery.");
    }
}
