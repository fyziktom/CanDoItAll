using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryEventOperationService(
    MemoryOperationCoordinator coordinator,
    IMemoryEventLedgerStore eventLedgerStore,
    IEnumerable<IMemoryProviderEventOutboxDriver> outboxDrivers)
{
    private readonly MemoryProviderDriverCatalog<IMemoryProviderEventOutboxDriver> driverCatalog =
        new(outboxDrivers, static driver => driver.DriverKind);

    public async Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeAsync(
        MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        MemoryOperationGuard.EnsureOperationKind(request.OperationKind, MemoryOperationKind.EventAcknowledge);
        var selection = await coordinator.SelectProviderAsync(
            request.SelectionPolicy,
            request.Caller.SelectionContext,
            cancellationToken);
        if (!selection.DispatchAllowed || selection.SelectedProvider is null)
        {
            return MemoryOperationResultFactory.Rejected<MemoryEventOutboxRecord>(selection);
        }

        var driver = driverCatalog.ResolveUnique(
            selection.SelectedProvider.DriverKind,
            out var driverFailure);
        if (driver is null)
        {
            return MemoryOperationResultFactory.DriverUnavailable<MemoryEventOutboxRecord>(
                selection,
                driverFailure);
        }

        var payload = MemoryPayload.FromJson(JsonSerializer.SerializeToElement(new
        {
            request.Payload.EventId,
            request.Payload.Accepted,
            Reason = MemoryOperationGuard.NormalizeReason(request.Payload.Reason)
        }));
        var outbox = MemoryEventOutboxRecord.CreateAcknowledgement(
            MemoryEventOutboxRecordId.New(),
            selection.SelectedProvider.InstanceId,
            request.Payload.EventId,
            inboxRecordId: null,
            coordinator.UtcNow,
            payload);
        await eventLedgerStore.EnqueueOutboxAsync(outbox, cancellationToken);
        return new MemoryOperationHandlerResult<MemoryEventOutboxRecord>(
            MemoryOperationHandlerStatus.Accepted,
            selection,
            OperationRecord: null,
            outbox,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            "Memory provider event acknowledgement queued.");
    }
}
