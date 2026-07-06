using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed partial class EfMemoryRetentionProjectionStore
{
    public async Task<MemoryRetentionApplicationResult> ApplyAsync(
        MemoryRetentionCandidate candidate,
        DateTimeOffset appliedAtUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.LedgerName switch
        {
            MemoryLedgerPersistenceContract.OperationRecords =>
                await ApplyOperationAsync(candidate, appliedAtUtc, reason, cancellationToken),
            MemoryLedgerPersistenceContract.FeedbackRecords =>
                await ApplyFeedbackAsync(candidate, appliedAtUtc, reason, cancellationToken),
            MemoryLedgerPersistenceContract.EventInboxRecords =>
                await ApplyInboxAsync(candidate, appliedAtUtc, reason, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported memory retention ledger '{candidate.LedgerName}'.")
        };
    }

    private async Task<MemoryRetentionApplicationResult> ApplyOperationAsync(
        MemoryRetentionCandidate candidate,
        DateTimeOffset appliedAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var operationId = Guid.Parse(candidate.RecordId);
        var entity = await dbContext.Set<MemoryOperationLedgerEntity>()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, cancellationToken)
            ?? throw new InvalidOperationException($"Memory operation '{candidate.RecordId}' was not found.");
        var applied = ApplyOperationRetention(
            entity.ToRecord(),
            candidate.Decision,
            appliedAtUtc,
            reason);
        entity.UpdateRecord(applied.Record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MemoryRetentionApplicationResult(
            candidate,
            applied.Record.Status,
            applied.IpfsUnpinRequested);
    }

    private async Task<MemoryRetentionApplicationResult> ApplyFeedbackAsync(
        MemoryRetentionCandidate candidate,
        DateTimeOffset appliedAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var feedbackRecordId = Guid.Parse(candidate.RecordId);
        var entity = await dbContext.Set<MemoryFeedbackLedgerEntity>()
            .SingleOrDefaultAsync(item => item.FeedbackRecordId == feedbackRecordId, cancellationToken)
            ?? throw new InvalidOperationException($"Memory feedback '{candidate.RecordId}' was not found.");
        var applied = ApplyFeedbackRetention(
            entity.ToRecord(),
            candidate.Decision,
            appliedAtUtc,
            reason);
        entity.UpdateRecord(applied.Record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MemoryRetentionApplicationResult(
            candidate,
            applied.Record.Status,
            applied.IpfsUnpinRequested);
    }

    private async Task<MemoryRetentionApplicationResult> ApplyInboxAsync(
        MemoryRetentionCandidate candidate,
        DateTimeOffset appliedAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inboxRecordId = Guid.Parse(candidate.RecordId);
        var entity = await dbContext.Set<MemoryEventInboxLedgerEntity>()
            .SingleOrDefaultAsync(item => item.InboxRecordId == inboxRecordId, cancellationToken)
            ?? throw new InvalidOperationException($"Memory event inbox '{candidate.RecordId}' was not found.");
        var applied = ApplyInboxRetention(
            entity.ToRecord(),
            candidate.Decision,
            appliedAtUtc,
            reason);
        entity.UpdateRecord(applied);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MemoryRetentionApplicationResult(candidate, applied.Status, false);
    }

    private static RetentionApplied<MemoryOperationRecord> ApplyOperationRetention(
        MemoryOperationRecord record,
        MemoryLedgerRetentionDecision decision,
        DateTimeOffset appliedAtUtc,
        string reason)
    {
        var unpinRequested = false;
        if (decision == MemoryLedgerRetentionDecision.Forget && record.IpfsSnapshot is { PinState: MemoryIpfsPinState.Pinned } ipfs)
        {
            record = record with
            {
                IpfsSnapshot = ipfs.RequestUnpin(appliedAtUtc, reason)
            };
            unpinRequested = true;
        }

        var applied = ApplyRetentionTransition(
            record,
            decision,
            appliedAtUtc,
            reason,
            MemoryLedgerTransitionRules.TransitionOperation);
        return new RetentionApplied<MemoryOperationRecord>(applied, unpinRequested);
    }

    private static RetentionApplied<MemoryFeedbackRecord> ApplyFeedbackRetention(
        MemoryFeedbackRecord record,
        MemoryLedgerRetentionDecision decision,
        DateTimeOffset appliedAtUtc,
        string reason)
    {
        var unpinRequested = false;
        if (decision == MemoryLedgerRetentionDecision.Forget && record.IpfsSnapshot is { PinState: MemoryIpfsPinState.Pinned } ipfs)
        {
            record = record with
            {
                IpfsSnapshot = ipfs.RequestUnpin(appliedAtUtc, reason)
            };
            unpinRequested = true;
        }

        var applied = ApplyRetentionTransition(
            record,
            decision,
            appliedAtUtc,
            reason,
            MemoryLedgerTransitionRules.TransitionFeedback);
        return new RetentionApplied<MemoryFeedbackRecord>(applied, unpinRequested);
    }

    private static MemoryEventInboxRecord ApplyInboxRetention(
        MemoryEventInboxRecord record,
        MemoryLedgerRetentionDecision decision,
        DateTimeOffset appliedAtUtc,
        string reason)
    {
        return ApplyRetentionTransition(
            record,
            decision,
            appliedAtUtc,
            reason,
            MemoryLedgerTransitionRules.TransitionInboxEvent);
    }

    private static TRecord ApplyRetentionTransition<TRecord>(
        TRecord record,
        MemoryLedgerRetentionDecision decision,
        DateTimeOffset appliedAtUtc,
        string reason,
        Func<TRecord, MemoryLedgerStatus, DateTimeOffset, string, TRecord> transition)
        where TRecord : notnull
    {
        var status = GetStatus(record);
        if (decision == MemoryLedgerRetentionDecision.Expire)
        {
            return IsActive(status)
                ? transition(record, MemoryLedgerStatus.Expired, appliedAtUtc, reason)
                : record;
        }

        if (status == MemoryLedgerStatus.Forgotten)
        {
            return record;
        }

        var expirable = IsActive(status)
            ? transition(record, MemoryLedgerStatus.Expired, appliedAtUtc, reason)
            : record;
        return transition(expirable, MemoryLedgerStatus.Forgotten, appliedAtUtc, reason);
    }

    private static MemoryLedgerStatus GetStatus<TRecord>(TRecord record) =>
        record switch
        {
            MemoryOperationRecord operation => operation.Status,
            MemoryFeedbackRecord feedback => feedback.Status,
            MemoryEventInboxRecord inbox => inbox.Status,
            _ => throw new NotSupportedException($"Unsupported memory retention record '{typeof(TRecord).Name}'.")
        };

    private static bool IsActive(MemoryLedgerStatus status) =>
        status is MemoryLedgerStatus.Pending or MemoryLedgerStatus.Accepted or MemoryLedgerStatus.Running;

    private sealed record RetentionApplied<TRecord>(
        TRecord Record,
        bool IpfsUnpinRequested);
}
