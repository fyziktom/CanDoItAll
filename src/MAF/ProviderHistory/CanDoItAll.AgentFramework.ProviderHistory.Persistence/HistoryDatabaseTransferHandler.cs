using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryDatabaseTransferHandler(IEnumerable<IHistoryTransferParticipant> participants) : IDatabaseTransferHandler {
    private readonly IHistoryTransferParticipant[] orderedParticipants = participants.OrderBy(item => item.Kind).ToArray();
    public const string TransferKey = "provider-request-history";
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        TransferKey, "Provider request history",
        "Copies history identities, policy, protected details, source mappings and replay state into an empty history partition.",
        25, true);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(DatabaseTransferContext context, CancellationToken cancellationToken = default) {
        var sourceCount = await context.SourceDbContext.Set<HistoryEntryRow>().CountAsync(cancellationToken);
        var targetCount = await context.TargetDbContext.Set<HistoryEntryRow>().CountAsync(cancellationToken);
        var available = await context.SourceDbContext.Set<HistoryStorageIdentity>().AnyAsync(cancellationToken)
            && await IsEmptyTargetAsync(context.TargetDbContext, cancellationToken);
        return new(Descriptor, available, $"{sourceCount} history entries available.", available
            ? "Transfer is a snapshot. Canonical source files and protection keys must remain accessible; they are not copied by this group."
            : "History transfer requires an initialized source and an empty target history partition.", sourceCount, targetCount);
    }

    public async Task<DatabaseTransferItemResult> TransferAsync(DatabaseTransferContext context, CancellationToken cancellationToken = default) {
        var source = context.SourceDbContext;
        var target = context.TargetDbContext;
        if (context.SourceProfile.Profile.Id == context.TargetProfile.Profile.Id ||
            !source.Database.IsRelational() || !target.Database.IsRelational()) {
            throw new InvalidOperationException("History transfer requires distinct relational database profiles.");
        }
        await using var sourceTransaction = await source.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using var targetTransaction = await target.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await target.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724091824013)", cancellationToken);
        if (!await source.Set<HistoryStorageIdentity>().AnyAsync(cancellationToken) ||
            !await IsEmptyTargetAsync(target, cancellationToken) ||
            !context.ReplaceExisting && await target.Set<HistoryStorageIdentity>().AnyAsync(cancellationToken)) {
            throw new InvalidOperationException("History transfer cannot merge or replace retained target history.");
        }
        foreach (var participant in orderedParticipants) {
            await participant.ValidateTargetAsync(context, cancellationToken);
        }
        await RemoveEmptyBootstrapAsync(target, cancellationToken);
        var count = await HistoryTransferBatch.CopyAsync(source.Set<HistoryPartitionRow>(), target, row => row.Id, cancellationToken);
        count += await HistoryTransferBatch.CopyAsync(source.Set<HistoryPolicyRow>(), target, row => row.PartitionId, cancellationToken);
        count += await HistoryTransferBatch.CopyAsync(source.Set<HistoryPolicyAuditRow>(), target, row => row.Id, cancellationToken);
        count += await HistoryTransferBatch.CopyAsync(source.Set<HistoryHostLeaseRow>(), target, row => row.Id, cancellationToken);
        count += await HistoryTransferBatch.CopyAsync(source.Set<HistorySourceRow>(), target, row => row.Id, cancellationToken);
        count += await HistoryTransferBatch.CopyAsync(source.Set<HistoryDetailRow>().Where(row => row.Part == HistoryDetailPart.Input),
            target, row => row.Id, cancellationToken);
        count += await HistoryTransferBatch.CopyAsync(source.Set<HistoryEntryRow>(), target, row => row.Id, cancellationToken);
        count += await HistoryTransferBatch.CopyAsync(source.Set<HistoryDetailRow>().Where(row => row.Part == HistoryDetailPart.Response),
            target, row => row.Id, cancellationToken);
        count += await HistoryTransferBatch.CopyOwnersAsync(source, target, cancellationToken);
        count += await HistoryTransferBatch.CopyAsync(source.Set<HistoryOutboxRow>(), target, row => row.Id, cancellationToken);
        foreach (var kind in Enum.GetValues<HistorySourceKind>()) {
            count += await HistoryTransferBatch.CopyAsync(source.Set<HistoryCheckpointRow>().Where(row => row.SourceKind == kind),
                target, row => row.PartitionId, cancellationToken);
        }
        foreach (var participant in orderedParticipants) {
            count = checked(count + await participant.CopyAsync(context, cancellationToken));
        }
        target.Add(await source.Set<HistoryStorageIdentity>().AsNoTracking().SingleAsync(cancellationToken));
        await target.SaveChangesAsync(cancellationToken);
        await targetTransaction.CommitAsync(cancellationToken);
        await sourceTransaction.CommitAsync(cancellationToken);
        return new(Descriptor.Key, Descriptor.Label, true, "Copied provider history without changing its storage lineage or recorded expiry.", checked(count + 1));
    }

    private static async Task<bool> IsEmptyTargetAsync(AppDbContext target, CancellationToken cancellationToken)
        => !await target.Set<HistoryEntryRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistorySourceRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryDetailRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryOutboxRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryHostLeaseRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryPolicyAuditRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryPolicyRow>().AnyAsync(row => row.Version != 0 || row.UsedDetailBytes != 0, cancellationToken);

    private static async Task RemoveEmptyBootstrapAsync(AppDbContext target, CancellationToken cancellationToken) {
        await target.Set<HistoryStorageIdentity>().ExecuteDeleteAsync(cancellationToken);
        await target.Set<HistoryCheckpointRow>().ExecuteDeleteAsync(cancellationToken);
        await target.Set<HistoryPolicyRow>().ExecuteDeleteAsync(cancellationToken);
        await target.Set<HistoryPartitionRow>().ExecuteDeleteAsync(cancellationToken);
        target.ChangeTracker.Clear();
    }
}
