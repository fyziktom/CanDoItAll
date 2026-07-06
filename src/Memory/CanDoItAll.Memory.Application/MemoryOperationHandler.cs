using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed partial class MemoryOperationHandler : IMemoryOperationHandler
{
    private readonly IMemoryProviderProfileStore providerProfileStore;
    private readonly IMemoryOperationLedgerStore operationLedgerStore;
    private readonly IMemoryFeedbackLedgerStore feedbackLedgerStore;
    private readonly IMemoryEventLedgerStore eventLedgerStore;
    private readonly IMemorySourceRequestLedgerStore sourceRequestLedgerStore;
    private readonly IMemorySourceGateway sourceGateway;
    private readonly IEnumerable<IMemoryProviderDriver> drivers;
    private readonly TimeProvider timeProvider;

    public MemoryOperationHandler(
        IMemoryProviderProfileStore providerProfileStore,
        IMemoryOperationLedgerStore operationLedgerStore,
        IMemoryFeedbackLedgerStore feedbackLedgerStore,
        IMemoryEventLedgerStore eventLedgerStore,
        IMemorySourceRequestLedgerStore sourceRequestLedgerStore,
        IMemorySourceGateway sourceGateway,
        IEnumerable<IMemoryProviderDriver> drivers,
        TimeProvider timeProvider)
    {
        this.providerProfileStore = providerProfileStore;
        this.operationLedgerStore = operationLedgerStore;
        this.feedbackLedgerStore = feedbackLedgerStore;
        this.eventLedgerStore = eventLedgerStore;
        this.sourceRequestLedgerStore = sourceRequestLedgerStore;
        this.sourceGateway = sourceGateway;
        this.drivers = drivers;
        this.timeProvider = timeProvider;
    }

    public async Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
        MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOperationKind(request.OperationKind, MemoryOperationKind.ContextQuery);
        if (!request.Payload.RequestedCapabilities.Contains(request.SelectionPolicy.RequiredCapability))
        {
            return CapabilityMismatch<MemoryContextPack>(
                request,
                $"Query requested capabilities do not include required capability '{request.SelectionPolicy.RequiredCapability}'.");
        }

        var selection = await SelectProviderAsync(request.SelectionPolicy, request.Caller.SelectionContext, cancellationToken);
        if (!selection.DispatchAllowed || selection.SelectedProvider is null)
        {
            return Rejected<MemoryContextPack>(selection);
        }

        var operation = await CreateOperationAsync(request, selection.SelectedProvider.InstanceId, cancellationToken);
        var driver = drivers.FirstOrDefault(candidate => candidate.DriverKind == selection.SelectedProvider.DriverKind);
        if (driver is null)
        {
            var failed = await operationLedgerStore.TransitionAsync(
                operation.OperationId,
                MemoryLedgerStatus.Failed,
                timeProvider.GetUtcNow(),
                $"No memory driver is registered for '{selection.SelectedProvider.DriverKind}'.",
                cancellationToken);
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.DriverUnavailable,
                selection,
                failed,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                failed.StatusReason);
        }

        var driverResult = await driver.ExecuteContextQueryAsync(
            selection.SelectedProvider,
            operation,
            request.Payload,
            cancellationToken);
        var output = driverResult.ContextPack is null
            ? null
            : EnsureFeedbackHandle(driverResult.ContextPack, operation.OperationId);
        var extensions = CreateQueryTransitionExtensions(operation, driverResult, output);
        var transitioned = await operationLedgerStore.TransitionAsync(
            operation.OperationId,
            driverResult.LedgerStatus,
            timeProvider.GetUtcNow(),
            driverResult.Diagnostic,
            extensions,
            cancellationToken);
        return new MemoryOperationHandlerResult<MemoryContextPack>(
            ToHandlerStatus(driverResult),
            selection,
            transitioned,
            output,
            driverResult.AcceptedOperation,
            output?.FeedbackHandle,
            DriverDispatchAttempted: true,
            driverResult.Diagnostic);
    }

    private static MemoryExtensionData CreateQueryTransitionExtensions(
        MemoryOperationRecord operation,
        MemoryProviderDriverResult driverResult,
        MemoryContextPack? output)
    {
        var extensions = operation.Extensions;
        if (driverResult.AcceptedOperation is not null)
        {
            extensions = extensions.WithAcceptedOperation(driverResult.AcceptedOperation);
        }

        if (output?.FeedbackHandle is { } feedbackHandle)
        {
            extensions = extensions.WithContextDelivery(output.ContextPackId, feedbackHandle);
        }

        return extensions;
    }
}
