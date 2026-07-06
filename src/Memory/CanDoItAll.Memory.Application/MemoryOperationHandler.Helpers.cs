using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed partial class MemoryOperationHandler
{
    private async Task<MemoryProviderSelectionResult> SelectProviderAsync(
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryProviderSelectionContext selectionContext,
        CancellationToken cancellationToken)
    {
        var profiles = await providerProfileStore.ListAsync(cancellationToken);
        return new InMemoryMemoryProviderRegistry(profiles)
            .SelectProvider(selectionPolicy, selectionContext);
    }

    private async Task<MemoryOperationRecord> CreateOperationAsync<TPayload>(
        MemoryOperationHandlerRequest<TPayload> request,
        MemoryProviderInstanceId providerInstanceId,
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
            timeProvider.GetUtcNow(),
            extensions: request.Extensions.WithMemoryOperationCaller(request.Caller));
        await operationLedgerStore.CreateAsync(operation, cancellationToken);
        return operation;
    }

    private static MemoryContextPack EnsureFeedbackHandle(
        MemoryContextPack contextPack,
        MemoryOperationId operationId)
    {
        if (contextPack.FeedbackHandle is not null)
        {
            return contextPack;
        }

        return contextPack with
        {
            FeedbackHandle = MemoryFeedbackHandle.Parse(
                $"memory-feedback:{operationId.Value:D}:{contextPack.ContextPackId.Value:D}")
        };
    }

    private static MemoryOperationHandlerResult<TOutput> CapabilityMismatch<TOutput>(
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

    private static MemoryOperationHandlerResult<TOutput> Rejected<TOutput>(
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

    private static MemoryOperationHandlerResult<TOutput> NotFound<TOutput>(
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

    private static MemoryOperationHandlerStatus ToHandlerStatus(
        MemoryProviderSelectionStatus selectionStatus) =>
        selectionStatus switch
        {
            MemoryProviderSelectionStatus.NoProviderConfigured => MemoryOperationHandlerStatus.NoProviderConfigured,
            MemoryProviderSelectionStatus.NoEnabledProvider => MemoryOperationHandlerStatus.NoEnabledProvider,
            MemoryProviderSelectionStatus.ProviderNotFound => MemoryOperationHandlerStatus.ProviderNotFound,
            MemoryProviderSelectionStatus.ProviderDisabled => MemoryOperationHandlerStatus.ProviderDisabled,
            MemoryProviderSelectionStatus.CapabilityUnavailable => MemoryOperationHandlerStatus.CapabilityUnavailable,
            MemoryProviderSelectionStatus.CapabilityDenied => MemoryOperationHandlerStatus.CapabilityDenied,
            _ => MemoryOperationHandlerStatus.Failed
        };

    private static MemoryOperationHandlerStatus ToHandlerStatus(
        MemoryProviderDriverResult driverResult) =>
        driverResult.Kind switch
        {
            MemoryProviderDriverResultKind.ContextPack => MemoryOperationHandlerStatus.Completed,
            MemoryProviderDriverResultKind.OperationAccepted => MemoryOperationHandlerStatus.Accepted,
            MemoryProviderDriverResultKind.Timeout => MemoryOperationHandlerStatus.TimedOut,
            MemoryProviderDriverResultKind.UnsupportedCapability => MemoryOperationHandlerStatus.UnsupportedOperation,
            _ => MemoryOperationHandlerStatus.Failed
        };

    private static void EnsureOperationKind(
        MemoryOperationKind actual,
        MemoryOperationKind expected)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Memory operation handler expected '{expected}' but received '{actual}'.");
        }
    }

    private static string NormalizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason must not be empty.", nameof(reason));
        }

        return reason.Trim();
    }
}
