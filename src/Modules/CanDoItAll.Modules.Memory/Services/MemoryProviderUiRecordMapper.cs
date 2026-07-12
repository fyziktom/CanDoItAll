using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.Memory.Services;

internal static class MemoryProviderUiRecordMapper
{
    public static MemoryProviderOperationUiRecord ToUiRecord(MemoryOperationRecord record) =>
        new(
            record.OperationId,
            record.ProviderInstanceId,
            record.RequestedCapability,
            record.OperationKind,
            record.Status,
            record.StatusReason,
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
            record.StatusReason,
            record.ReceivedAtUtc,
            record.UpdatedAtUtc);
}
