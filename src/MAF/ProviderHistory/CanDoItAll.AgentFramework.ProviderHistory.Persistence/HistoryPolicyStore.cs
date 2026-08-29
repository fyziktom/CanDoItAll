using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryPolicyStore(
    IDbContextFactory<AppDbContext> factory,
    IProviderHistoryAccess access,
    TimeProvider clock,
    HistoryAuthorizedOperation operations,
    IDatabaseRuntimeState runtime,
    IDatabaseRuntimeWriteFence writeFence) : IProviderHistoryPolicyService {
    public Task<HistoryPolicySnapshot> GetAsync(CancellationToken cancellationToken) =>
        operations.RunAsync(HistoryPermission.Manage, async (context, token) => {
            await using var db = await factory.CreateDbContextAsync(token);
            await HistoryPartitionStore.RequireAsync(db, context.Partition, token);
            var row = await db.Set<HistoryPolicyRow>().AsNoTracking()
                .SingleAsync(row => row.PartitionId == context.Partition.StorageLineageId, token);
            return Snapshot(row);
        }, cancellationToken);

    public Task<HistoryRetentionPreview> PreviewShorterRetentionAsync(HistoryPolicy policy, CancellationToken cancellationToken) {
        HistoryContractValidation.Validate(policy);
        return operations.RunAsync(HistoryPermission.Manage, async (context, token) => {
            await using var db = await factory.CreateDbContextAsync(token);
            await HistoryPartitionStore.RequireAsync(db, context.Partition, token);
            return await HistoryPolicyRetention.PreviewAsync(db, context.Partition.StorageLineageId, policy, token);
        }, cancellationToken);
    }

    public Task<HistoryPolicySnapshot> UpdateAsync(HistoryPolicyUpdate update, CancellationToken cancellationToken) {
        HistoryContractValidation.Validate(update.Policy);
        return operations.RunAsync(HistoryPermission.Manage, async (context, token) => {
            var expected = runtime.GetSnapshot();
            if (expected.Generation != context.Fence.ProfileGeneration) {
                throw Stale();
            }
            try {
                return await writeFence.ExecuteAsync(expected, inner => UpdateCoreAsync(context, update, inner), token);
            } catch (DatabaseRuntimeProfileChangedException) {
                throw Stale();
            }
        }, cancellationToken);
    }

    private async Task<HistoryPolicySnapshot> UpdateCoreAsync(HistoryAccessContext context, HistoryPolicyUpdate update, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, context.Partition, cancellationToken);
        var row = await LockAsync(db, context.Partition.StorageLineageId, cancellationToken);
        if (row.Version != update.ExpectedVersion) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "The history policy was changed by another operator.");
        }
        if (update.ApplyShorterRetention) {
            await HistoryPolicyRetention.ShortenAsync(db, row.PartitionId, update.Policy, cancellationToken);
        }
        row.CaptureMode = update.Policy.CaptureMode;
        row.MetadataRetentionDays = update.Policy.MetadataRetentionDays;
        row.DetailRetentionDays = update.Policy.DetailRetentionDays;
        row.MaximumTextBytes = update.Policy.MaximumTextBytes;
        row.DetailQuotaBytes = update.Policy.DetailQuotaBytes;
        row.BatchSize = update.Policy.BatchSize;
        row.Version = checked(row.Version + 1);
        db.Add(new HistoryPolicyAuditRow {
            PartitionId = row.PartitionId, Version = row.Version, ChangedAtUtc = clock.GetUtcNow(),
            Policy = update.Policy, AppliedShorterRetention = update.ApplyShorterRetention, Caller = context.Caller
        });
        await access.EnsureCurrentAsync(context, HistoryPermission.Manage, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await access.EnsureCurrentAsync(context, HistoryPermission.Manage, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Snapshot(row);
    }

    internal static Task<HistoryPolicyRow> LockAsync(AppDbContext db, Guid partitionId, CancellationToken cancellationToken)
        => db.Set<HistoryPolicyRow>().FromSqlInterpolated(
            $"""SELECT * FROM "ProviderHistory_Policies" WHERE "PartitionId" = {partitionId} FOR UPDATE""")
            .SingleAsync(cancellationToken);

    internal static HistoryPolicySnapshot Snapshot(HistoryPolicyRow row) => new(new HistoryPolicy {
        CaptureMode = row.CaptureMode, MetadataRetentionDays = row.MetadataRetentionDays,
        DetailRetentionDays = row.DetailRetentionDays, MaximumTextBytes = row.MaximumTextBytes,
        DetailQuotaBytes = row.DetailQuotaBytes, BatchSize = row.BatchSize
    }, row.Version);

    private static ProviderHistoryException Stale() =>
        new(HistoryFailure.StaleContext, "The active database changed before the history policy could commit.");
}
