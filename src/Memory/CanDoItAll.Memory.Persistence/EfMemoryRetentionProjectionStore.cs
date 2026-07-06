using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Memory.Persistence;

public sealed partial class EfMemoryRetentionProjectionStore(
    IDbContextFactory<AppDbContext> dbContextFactory) : IMemoryRetentionProjectionStore
{
    public async Task<IReadOnlyList<MemoryRetentionCandidate>> ListDueAsync(
        DateTimeOffset nowUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Retention candidate page size must be positive.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var operationCandidates = await dbContext.Set<MemoryOperationLedgerEntity>()
            .AsNoTracking()
            .Where(entity => entity.Status != (int)MemoryLedgerStatus.Forgotten)
            .Where(entity => entity.ExpiresAtUtc <= nowUtc || entity.ForgetAtUtc <= nowUtc)
            .OrderBy(entity => entity.ForgetAtUtc <= nowUtc ? entity.ForgetAtUtc : entity.ExpiresAtUtc)
            .Take(take)
            .Select(entity => new RetentionProjection(
                MemoryLedgerPersistenceContract.OperationRecords,
                entity.OperationId.ToString(),
                entity.ExpiresAtUtc,
                entity.ForgetAtUtc))
            .ToArrayAsync(cancellationToken);
        var feedbackCandidates = await dbContext.Set<MemoryFeedbackLedgerEntity>()
            .AsNoTracking()
            .Where(entity => entity.Status != (int)MemoryLedgerStatus.Forgotten)
            .Where(entity => entity.ExpiresAtUtc <= nowUtc || entity.ForgetAtUtc <= nowUtc)
            .OrderBy(entity => entity.ForgetAtUtc <= nowUtc ? entity.ForgetAtUtc : entity.ExpiresAtUtc)
            .Take(take)
            .Select(entity => new RetentionProjection(
                MemoryLedgerPersistenceContract.FeedbackRecords,
                entity.FeedbackRecordId.ToString(),
                entity.ExpiresAtUtc,
                entity.ForgetAtUtc))
            .ToArrayAsync(cancellationToken);
        var eventInboxCandidates = await dbContext.Set<MemoryEventInboxLedgerEntity>()
            .AsNoTracking()
            .Where(entity => entity.Status != (int)MemoryLedgerStatus.Forgotten)
            .Where(entity => entity.ExpiresAtUtc <= nowUtc || entity.ForgetAtUtc <= nowUtc)
            .OrderBy(entity => entity.ForgetAtUtc <= nowUtc ? entity.ForgetAtUtc : entity.ExpiresAtUtc)
            .Take(take)
            .Select(entity => new RetentionProjection(
                MemoryLedgerPersistenceContract.EventInboxRecords,
                entity.InboxRecordId.ToString(),
                entity.ExpiresAtUtc,
                entity.ForgetAtUtc))
            .ToArrayAsync(cancellationToken);

        return operationCandidates
            .Concat(feedbackCandidates)
            .Concat(eventInboxCandidates)
            .Select(candidate => ToCandidate(candidate, nowUtc))
            .OrderBy(candidate => candidate.DueAtUtc)
            .Take(take)
            .ToArray();
    }

    private static MemoryRetentionCandidate ToCandidate(
        RetentionProjection projection,
        DateTimeOffset nowUtc)
    {
        var decision = nowUtc >= projection.ForgetAtUtc
            ? MemoryLedgerRetentionDecision.Forget
            : MemoryLedgerRetentionDecision.Expire;
        var dueAtUtc = decision == MemoryLedgerRetentionDecision.Forget
            ? projection.ForgetAtUtc
            : projection.ExpiresAtUtc;
        return new MemoryRetentionCandidate(
            projection.LedgerName,
            projection.RecordId,
            decision,
            dueAtUtc);
    }

    private sealed record RetentionProjection(
        string LedgerName,
        string RecordId,
        DateTimeOffset ExpiresAtUtc,
            DateTimeOffset ForgetAtUtc);
}
