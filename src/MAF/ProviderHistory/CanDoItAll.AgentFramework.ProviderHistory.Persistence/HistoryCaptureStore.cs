using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryCaptureStore(
    IDbContextFactory<AppDbContext> factory,
    IDatabaseRuntimeState runtime,
    IDatabaseRuntimeWriteFence writeFence,
    HistoryDetailStore details,
    HistoryHostLeaseStore hostLease) : IProviderHistoryCapture {
    public async Task BeginAsync(HistoryAttemptStart start, HistoryCurrentTurn? currentTurn, CancellationToken cancellationToken) {
        HistoryContractValidation.Validate(start);
        var expected = runtime.GetSnapshot();
        if (expected.Generation != start.Fence.ProfileGeneration) {
            throw new ProviderHistoryException(HistoryFailure.StaleContext, "The provider request belongs to an earlier runtime generation.");
        }
        var hostId = await hostLease.EnsureAsync(start.Partition, cancellationToken);
        var detail = await details.PrepareAsync(start, currentTurn?.Input, HistoryDetailPart.Input,
            currentTurn?.InputRevision ?? 0, cancellationToken);
        await writeFence.ExecuteAsync(expected, async token => {
            await using var db = await factory.CreateDbContextAsync(token);
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            await HistoryPartitionStore.RequireAsync(db, start.Partition, token);
            await HistoryWriteLock.AttemptAsync(db, start.AttemptId.Value, token);
            var policy = detail.Row is null ? null : await HistoryPolicyStore.LockAsync(db, start.Partition.StorageLineageId, token);
            var existing = await db.Set<HistoryEntryRow>().SingleOrDefaultAsync(
                row => row.PartitionId == start.Partition.StorageLineageId && row.AttemptId == start.AttemptId.Value, token);
            if (existing is not null) {
                RequireSameAttempt(existing, start);
                return true;
            }
            var entry = HistoryEntryMapping.Started(start);
            entry.DetailState = detail.State;
            entry.CaptureHostId = hostId;
            db.Add(entry);
            if (start.ContentOwner is { } owner) {
                await HistorySourceProjection.ReserveAsync(db, entry, owner, token);
            }
            if (detail.Row is { } body) {
                await HistoryDetailStore.AttachAsync(db, entry, body, policy!, token);
            }
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return true;
        }, cancellationToken);
    }

    public async Task CompleteAsync(HistoryAttemptStart start, HistoryAttemptCompletion completion,
        string? currentResponse, CancellationToken cancellationToken) {
        if (completion.Outcome is HistoryOutcome.Started or HistoryOutcome.Unknown || completion.FinishedAtUtc < start.StartedAtUtc) {
            throw new ArgumentException("A provider completion must have a terminal outcome and a valid finish time.", nameof(completion));
        }
        ValidateUsage(completion);
        completion = completion with { FinishedAtUtc = HistoryStorageTimestamp.Normalize(completion.FinishedAtUtc) };
        var detail = await details.PrepareAsync(start, currentResponse, HistoryDetailPart.Response, 0, cancellationToken);
        if (detail.Row is { } captured && completion.ResponseOriginalBytes > captured.OriginalBytes) {
            captured.OriginalBytes = completion.ResponseOriginalBytes.Value;
            captured.Flags |= HistoryDetailFlags.Truncated;
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, start.Partition, cancellationToken);
        await HistoryWriteLock.AttemptAsync(db, start.AttemptId.Value, cancellationToken);
        var policy = detail.Row is null ? null
            : await HistoryPolicyStore.LockAsync(db, start.Partition.StorageLineageId, cancellationToken);
        var entry = await db.Set<HistoryEntryRow>().SingleOrDefaultAsync(
            row => row.PartitionId == start.Partition.StorageLineageId && row.AttemptId == start.AttemptId.Value,
            cancellationToken) ?? throw new ProviderHistoryException(HistoryFailure.Conflict, "The durable provider attempt was not started.");
        RequireSameAttempt(entry, start);
        var persisted = HistoryEntryMapping.ToEntry(entry, start.Partition);
        var existing = entry.Outcome == HistoryOutcome.Started ? null : new HistoryAttemptCompletion(
            persisted.Outcome, persisted.FinishedAtUtc!.Value, persisted.Usage, persisted.Price, persisted.RemoteRequest);
        if (!HistoryCompletionTransitions.ShouldApply(existing, completion)) {
            return;
        }
        HistoryEntryMapping.Complete(entry, completion);
        if (detail.Row is { } body && entry.DetailState is not (HistoryDetailState.Canonical or HistoryDetailState.Deleted)) {
            await HistoryDetailStore.AttachAsync(db, entry, body, policy!, cancellationToken);
        } else if (entry.DetailState is not (HistoryDetailState.Captured or HistoryDetailState.Canonical or HistoryDetailState.Deleted)) {
            entry.DetailState = detail.State;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void RequireSameAttempt(HistoryEntryRow row, HistoryAttemptStart start) {
        if (row.Id != start.EntryId.Value || row.RequestId != start.RequestId.Value ||
            row.ProviderId != start.Provider.Id?.Value || row.ResolvedModel != start.Provider.ResolvedModel?.Value ||
            row.StartedAtUtc != HistoryStorageTimestamp.Normalize(start.StartedAtUtc) || row.CredentialId != start.Caller.CredentialId?.Value) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "A provider attempt identity was reused with different immutable facts.");
        }
    }

    private static void ValidateUsage(HistoryAttemptCompletion completion) {
        var usage = completion.Usage;
        if (!Enum.IsDefined(completion.Outcome) || !Enum.IsDefined(usage.State) ||
            usage.InputTokens < 0 || usage.OutputTokens < 0 || usage.CachedInputTokens < 0 ||
            usage.CacheWriteTokens < 0 || usage.ReasoningTokens < 0 || usage.ImageCount < 0 ||
            usage.CachedInputTokens > usage.InputTokens || usage.CacheWriteTokens > usage.InputTokens ||
            usage.ReasoningTokens > usage.OutputTokens || completion.Price.Amount < 0) {
            throw new ArgumentException("Provider completion evidence contains invalid usage or price.", nameof(completion));
        }
    }
}
