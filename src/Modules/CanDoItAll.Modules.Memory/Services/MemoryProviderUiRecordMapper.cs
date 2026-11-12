using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

internal static class MemoryProviderUiRecordMapper
{
    public static string SafeDiagnostic(MemoryOperationHandlerStatus status, string diagnostic) => status is
        MemoryOperationHandlerStatus.DriverFailed or MemoryOperationHandlerStatus.DriverUnavailable or MemoryOperationHandlerStatus.ProviderConfigurationFailed or MemoryOperationHandlerStatus.Failed
        ? $"Provider request failed ({status}). Review the operation identity and provider configuration before retrying."
        : diagnostic;

    public static MemoryProviderActionStatus ToUiStatus(MemoryOperationHandlerStatus status) => status switch {
        MemoryOperationHandlerStatus.Completed => MemoryProviderActionStatus.Completed,
        MemoryOperationHandlerStatus.Accepted => MemoryProviderActionStatus.Accepted,
        MemoryOperationHandlerStatus.NoProviderConfigured => MemoryProviderActionStatus.NoProviderConfigured,
        MemoryOperationHandlerStatus.NoEnabledProvider => MemoryProviderActionStatus.NoEnabledProvider,
        MemoryOperationHandlerStatus.ProviderNotFound => MemoryProviderActionStatus.ProviderNotFound,
        MemoryOperationHandlerStatus.ProviderDisabled => MemoryProviderActionStatus.ProviderDisabled,
        MemoryOperationHandlerStatus.CapabilityUnavailable => MemoryProviderActionStatus.CapabilityUnavailable,
        MemoryOperationHandlerStatus.CapabilityDenied => MemoryProviderActionStatus.CapabilityDenied,
        MemoryOperationHandlerStatus.CapabilityMismatch => MemoryProviderActionStatus.CapabilityMismatch,
        MemoryOperationHandlerStatus.DriverUnavailable => MemoryProviderActionStatus.DriverUnavailable,
        MemoryOperationHandlerStatus.SourceCaptureFailed => MemoryProviderActionStatus.SourceCaptureFailed,
        MemoryOperationHandlerStatus.NotFound => MemoryProviderActionStatus.NotFound,
        MemoryOperationHandlerStatus.Cancelled => MemoryProviderActionStatus.Cancelled,
        MemoryOperationHandlerStatus.Failed => MemoryProviderActionStatus.Failed,
        MemoryOperationHandlerStatus.TimedOut => MemoryProviderActionStatus.TimedOut,
        MemoryOperationHandlerStatus.UnsupportedOperation => MemoryProviderActionStatus.UnsupportedOperation,
        MemoryOperationHandlerStatus.ProviderDenied => MemoryProviderActionStatus.ProviderDenied,
        MemoryOperationHandlerStatus.ProviderSelectionRequired => MemoryProviderActionStatus.ProviderSelectionRequired,
        MemoryOperationHandlerStatus.AccessDenied => MemoryProviderActionStatus.AccessDenied,
        MemoryOperationHandlerStatus.ProviderConfigurationFailed => MemoryProviderActionStatus.ProviderConfigurationFailed,
        MemoryOperationHandlerStatus.DriverFailed => MemoryProviderActionStatus.DriverFailed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown operation status.")
    };

    public static MemoryProviderOperationUiRecord ToUiRecord(MemoryOperationRecord record) =>
        new(
            record.OperationId,
            record.ProviderInstanceId,
            record.RequestedCapability,
            record.OperationKind,
            record.Status,
            record.Status == MemoryLedgerStatus.Failed ? "Provider operation failed. Review its exact identity before retrying." : record.StatusReason,
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            record.CompletedAtUtc,
            record.Extensions.GetAcceptedOperation(),
            record.Extensions.GetContextDelivery()?.FeedbackHandle);

    public static MemoryProviderFeedbackUiRecord ToUiRecord(MemoryFeedbackRecord record) =>
        new(
            record.FeedbackRecordId,
            record.ProviderInstanceId,
            record.Stage,
            record.Outcome,
            record.MatchState,
            record.Status,
            record.UnmatchedReason,
            record.CreatedAtUtc,
            record.UpdatedAtUtc);

    public static MemoryProviderEventUiRecord ToUiRecord(MemoryEventInboxRecord record) =>
        new(
            record.InboxRecordId,
            record.ProviderInstanceId,
            record.ProviderEventId,
            record.EventKind,
            record.Priority,
            record.Status,
            record.Status == MemoryLedgerStatus.Failed ? "Provider operation failed. Review its exact identity before retrying." : record.StatusReason,
            record.ReceivedAtUtc,
            record.UpdatedAtUtc);
}
