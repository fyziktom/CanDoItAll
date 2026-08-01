using CanDoItAll.Memory.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryQueryOperationService(
    MemoryOperationCoordinator coordinator,
    IEnumerable<IMemoryProviderDriver> drivers,
    IEnumerable<IMemoryProviderFeedbackDeliveryDriver> feedbackDrivers,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger logger = loggerFactory.CreateLogger<MemoryQueryOperationService>();
    private readonly MemoryProviderDriverCatalog<IMemoryProviderDriver> driverCatalog =
        new(drivers, static driver => driver.DriverKind);
    private readonly MemoryProviderDriverCatalog<IMemoryProviderFeedbackDeliveryDriver> feedbackDriverCatalog =
        new(feedbackDrivers, static driver => driver.DriverKind);

    public async Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteAsync(
        MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        MemoryOperationGuard.EnsureOperationKind(request.OperationKind, MemoryOperationKind.ContextQuery);
        if (!request.Payload.RequestedCapabilities.Contains(request.SelectionPolicy.RequiredCapability))
        {
            return MemoryOperationResultFactory.CapabilityMismatch<MemoryContextPack>(
                request,
                $"Query requested capabilities do not include required capability '{request.SelectionPolicy.RequiredCapability}'.");
        }

        MemoryProviderSelectionResult selection;
        try
        {
            selection = await coordinator.SelectProviderAsync(
                request.SelectionPolicy,
                request.Caller.SelectionContext,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Memory provider selection failed for capability {CapabilityId}. ExceptionType={ExceptionType}",
                request.SelectionPolicy.RequiredCapability.Value,
                exception.GetType().Name);
            return MemoryOperationResultFactory.Rejected<MemoryContextPack>(
                MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.ProviderConfigurationFailed,
                    MemoryProviderSelectionReason.None,
                    request.SelectionPolicy.RequiredCapability,
                    "Memory provider configuration could not be loaded; dispatch was not attempted.",
                    []));
        }

        if (!selection.DispatchAllowed || selection.SelectedProvider is null)
        {
            return MemoryOperationResultFactory.Rejected<MemoryContextPack>(selection);
        }

        var operation = await coordinator.CreateQueryOperationAsync(
            request,
            selection.SelectedProvider.InstanceId,
            cancellationToken);
        var driver = driverCatalog.ResolveUnique(
            selection.SelectedProvider.DriverKind,
            out var driverFailure);
        if (driver is null)
        {
            var failed = await coordinator.OperationLedgerStore.TransitionAsync(
                operation.OperationId,
                MemoryLedgerStatus.Failed,
                coordinator.UtcNow,
                driverFailure,
                cancellationToken);
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.DriverUnavailable,
                selection,
                failed,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                driverFailure);
        }

        MemoryProviderDriverResult driverResult;
        try
        {
            driverResult = await driver.ExecuteContextQueryAsync(
                selection.SelectedProvider,
                operation,
                request.Payload,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Memory query dispatch failed. ProviderInstanceId={ProviderInstanceId} DriverKind={DriverKind} OperationId={OperationId} ExceptionType={ExceptionType}",
                selection.SelectedProvider.InstanceId.Value,
                selection.SelectedProvider.DriverKind,
                operation.OperationId.Value,
                exception.GetType().Name);
            const string diagnostic = "The memory provider query failed before returning a valid result.";
            var failed = await coordinator.OperationLedgerStore.TransitionAsync(
                operation.OperationId,
                MemoryLedgerStatus.Failed,
                coordinator.UtcNow,
                diagnostic,
                cancellationToken);
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.DriverFailed,
                selection,
                failed,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: true,
                diagnostic);
        }

        var transitionedAt = coordinator.UtcNow;
        if (driverResult.AcceptedOperation is { } providerAcceptedOperation &&
            MemoryAcceptedOperationValidator.GetFailure(
                operation,
                providerAcceptedOperation,
                transitionedAt) is { } acceptanceFailure)
        {
            var failed = await coordinator.OperationLedgerStore.TransitionAsync(
                operation.OperationId,
                MemoryLedgerStatus.Failed,
                transitionedAt,
                acceptanceFailure,
                cancellationToken);
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.DriverFailed,
                selection,
                failed,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: true,
                acceptanceFailure);
        }

        if (driverResult.ContextPack is { } contextPack &&
            MemoryContextPackValidator.GetFailure(
                contextPack,
                request.Payload.Context.Budget,
                selection.SelectedProvider.Manifest.Limits) is { } contextPackFailure)
        {
            var failed = await coordinator.OperationLedgerStore.TransitionAsync(
                operation.OperationId,
                MemoryLedgerStatus.Failed,
                transitionedAt,
                contextPackFailure,
                cancellationToken);
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.DriverFailed,
                selection,
                failed,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: true,
                contextPackFailure);
        }

        var output = driverResult.ContextPack is null
            ? null
            : MemoryContextPackFeedbackPolicy.Normalize(
                driverResult.ContextPack,
                operation.OperationId,
                selection.SelectedProvider,
                feedbackDriverCatalog.ResolveUnique(
                    selection.SelectedProvider.DriverKind,
                    out _) is not null);
        var acceptedOperation = driverResult.AcceptedOperation is null
            ? null
            : MemoryAcceptedOperationValidator.CreateHostFacing(driverResult.AcceptedOperation);
        var extensions = acceptedOperation is null
            ? operation.Extensions
            : operation.Extensions.WithAcceptedOperation(acceptedOperation);
        if (output?.FeedbackHandle is { } feedbackHandle)
        {
            extensions = extensions.WithContextDelivery(output.ContextPackId, feedbackHandle);
        }

        var transitioned = await coordinator.OperationLedgerStore.TransitionAsync(
            operation.OperationId,
            driverResult.LedgerStatus,
            transitionedAt,
            driverResult.Diagnostic,
            extensions,
            cancellationToken);
        return new MemoryOperationHandlerResult<MemoryContextPack>(
            MemoryOperationResultFactory.ToHandlerStatus(driverResult),
            selection,
            transitioned,
            output,
            acceptedOperation,
            output?.FeedbackHandle,
            DriverDispatchAttempted: true,
            driverResult.Diagnostic);
    }

}
