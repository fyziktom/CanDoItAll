using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryOperationCoordinator(
    IMemoryProviderProfileStore providerProfileStore,
    IMemoryOperationLedgerStore operationLedgerStore,
    TimeProvider timeProvider)
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public IMemoryOperationLedgerStore OperationLedgerStore => operationLedgerStore;

    public async Task<MemoryProviderSelectionResult> SelectProviderAsync(
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryProviderSelectionContext selectionContext,
        CancellationToken cancellationToken)
    {
        var profiles = await providerProfileStore.ListAsync(cancellationToken);
        return new InMemoryMemoryProviderRegistry(profiles)
            .SelectProvider(selectionPolicy, selectionContext);
    }

    public async Task<MemoryOperationRecord> CreateOperationAsync<TPayload>(
        MemoryOperationHandlerRequest<TPayload> request,
        MemoryProviderInstanceId providerInstanceId,
        CancellationToken cancellationToken) =>
        await CreateOperationAsync(
            request,
            providerInstanceId,
            requestContext: null,
            cancellationToken);

    public async Task<MemoryOperationRecord> CreateQueryOperationAsync(
        MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
        MemoryProviderInstanceId providerInstanceId,
        CancellationToken cancellationToken) =>
        await CreateOperationAsync(
            request,
            providerInstanceId,
            request.Payload.Context,
            cancellationToken);

    private async Task<MemoryOperationRecord> CreateOperationAsync<TPayload>(
        MemoryOperationHandlerRequest<TPayload> request,
        MemoryProviderInstanceId providerInstanceId,
        MemoryRequestContext? requestContext,
        CancellationToken cancellationToken)
    {
        var operation = MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            providerInstanceId,
            request.SelectionPolicy.RequiredCapability,
            request.OperationKind,
            request.Caller.Requester,
            request.CorrelationId,
            request.CausationId,
            request.SourceSnapshotIds,
            request.Retention,
            UtcNow,
            extensions: request.Extensions.WithMemoryOperationCaller(request.Caller));
        if (requestContext is not null)
        {
            operation = operation with
            {
                Extensions = operation.Extensions.WithMemoryRequestContext(operation, requestContext)
            };
        }

        await operationLedgerStore.CreateAsync(operation, cancellationToken);
        return operation;
    }
}

internal static class MemoryOperationResultFactory
{
    public static MemoryOperationHandlerResult<TOutput> CapabilityMismatch<TOutput>(
        MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
        string diagnostic)
    {
        var selection = MemoryProviderSelectionResult.Rejected(
            MemoryProviderSelectionStatus.CapabilityDenied,
            MemoryProviderSelectionReason.None,
            request.SelectionPolicy.RequiredCapability,
            diagnostic,
            []);
        return new MemoryOperationHandlerResult<TOutput>(
            MemoryOperationHandlerStatus.CapabilityMismatch,
            selection,
            OperationRecord: null,
            Output: default,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            diagnostic);
    }

    public static MemoryOperationHandlerResult<TOutput> Rejected<TOutput>(
        MemoryProviderSelectionResult selection)
    {
        return new MemoryOperationHandlerResult<TOutput>(
            ToHandlerStatus(selection.Status),
            selection,
            OperationRecord: null,
            Output: default,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            selection.Diagnostic);
    }

    public static MemoryOperationHandlerResult<TOutput> NotFound<TOutput>(
        MemoryProviderSelectionPolicy selectionPolicy,
        string diagnostic)
    {
        var selection = MemoryProviderSelectionResult.Rejected(
            MemoryProviderSelectionStatus.ProviderNotFound,
            MemoryProviderSelectionReason.None,
            selectionPolicy.RequiredCapability,
            diagnostic,
            []);
        return new MemoryOperationHandlerResult<TOutput>(
            MemoryOperationHandlerStatus.NotFound,
            selection,
            OperationRecord: null,
            Output: default,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            diagnostic);
    }

    public static MemoryOperationHandlerResult<TOutput> DriverUnavailable<TOutput>(
        MemoryProviderSelectionResult selection,
        string diagnostic)
    {
        return new MemoryOperationHandlerResult<TOutput>(
            MemoryOperationHandlerStatus.DriverUnavailable,
            selection,
            OperationRecord: null,
            Output: default,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            diagnostic);
    }

    public static MemoryOperationHandlerResult<TOutput> AccessDenied<TOutput>(
        MemoryProviderSelectionPolicy selectionPolicy)
    {
        const string diagnostic = "The caller is not authorized to access this memory operation.";
        var selection = MemoryProviderSelectionResult.Rejected(
            MemoryProviderSelectionStatus.ProviderDenied,
            MemoryProviderSelectionReason.None,
            selectionPolicy.RequiredCapability,
            diagnostic,
            []);
        return new MemoryOperationHandlerResult<TOutput>(
            MemoryOperationHandlerStatus.AccessDenied,
            selection,
            OperationRecord: null,
            Output: default,
            AcceptedOperation: null,
            FeedbackHandle: null,
            DriverDispatchAttempted: false,
            diagnostic);
    }

    public static MemoryOperationHandlerStatus ToHandlerStatus(
        MemoryProviderSelectionStatus selectionStatus) =>
        selectionStatus switch
        {
            MemoryProviderSelectionStatus.NoProviderConfigured => MemoryOperationHandlerStatus.NoProviderConfigured,
            MemoryProviderSelectionStatus.NoEnabledProvider => MemoryOperationHandlerStatus.NoEnabledProvider,
            MemoryProviderSelectionStatus.ProviderNotFound => MemoryOperationHandlerStatus.ProviderNotFound,
            MemoryProviderSelectionStatus.ProviderDisabled => MemoryOperationHandlerStatus.ProviderDisabled,
            MemoryProviderSelectionStatus.CapabilityUnavailable => MemoryOperationHandlerStatus.CapabilityUnavailable,
            MemoryProviderSelectionStatus.CapabilityDenied => MemoryOperationHandlerStatus.CapabilityDenied,
            MemoryProviderSelectionStatus.ProviderDenied => MemoryOperationHandlerStatus.ProviderDenied,
            MemoryProviderSelectionStatus.ProviderSelectionRequired => MemoryOperationHandlerStatus.ProviderSelectionRequired,
            MemoryProviderSelectionStatus.ProviderConfigurationFailed => MemoryOperationHandlerStatus.ProviderConfigurationFailed,
            _ => MemoryOperationHandlerStatus.Failed
        };

    public static MemoryOperationHandlerStatus ToHandlerStatus(
        MemoryProviderDriverResult driverResult) =>
        driverResult.Kind switch
        {
            MemoryProviderDriverResultKind.ContextPack => MemoryOperationHandlerStatus.Completed,
            MemoryProviderDriverResultKind.OperationAccepted => MemoryOperationHandlerStatus.Accepted,
            MemoryProviderDriverResultKind.Timeout => MemoryOperationHandlerStatus.TimedOut,
            MemoryProviderDriverResultKind.UnsupportedCapability => MemoryOperationHandlerStatus.UnsupportedOperation,
            _ => MemoryOperationHandlerStatus.Failed
        };
}

internal static class MemoryOperationGuard
{
    public static void EnsureOperationKind(
        MemoryOperationKind actual,
        MemoryOperationKind expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Memory operation handler expected '{expected}' but received '{actual}'.");
        }
    }

    public static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason must not be empty.", nameof(reason));
        }

        return reason.Trim();
    }
}
