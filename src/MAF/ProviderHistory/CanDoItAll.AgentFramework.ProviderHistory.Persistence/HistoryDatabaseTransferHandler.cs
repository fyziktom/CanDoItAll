using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryDatabaseTransferHandler(IEnumerable<IHistoryTransferParticipant> participants,
    DatabaseTransferOwnerSessionRunner sessions, DatabaseTransferOperationRunner operations) : IDatabaseTransferHandler {
    private readonly IHistoryTransferParticipant[] orderedParticipants = participants.OrderBy(item => item.Kind).ToArray();
    public const string TransferKey = "provider-request-history";
    public DatabaseTransferItemDescriptor Descriptor { get; } = new(
        TransferKey, "Provider request history",
        "Copies history identities, policy, protected details, source mappings and replay state into an empty history partition.",
        25, true);

    public async Task<DatabaseTransferItemPreview> PreviewAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default) {
        var sourceFacts = await operations.RunIndependentAsync(context.SourceProfile, async (session, token) => {
            await using var source = await operations.CreateOwnerAsync<ProviderHistoryDbContext>(session, static options => new ProviderHistoryDbContext(options), token);
            return (Count: await source.Set<HistoryEntryRow>().CountAsync(token),
                Initialized: await source.Set<HistoryStorageIdentity>().AnyAsync(token));
        }, cancellationToken);
        var targetFacts = await operations.RunIndependentAsync(context.TargetProfile, async (session, token) => {
            await using var target = await operations.CreateOwnerAsync<ProviderHistoryDbContext>(session, static options => new ProviderHistoryDbContext(options), token);
            return (Count: await target.Set<HistoryEntryRow>().CountAsync(token), Empty: await IsEmptyTargetAsync(target, token));
        }, cancellationToken);
        var sourceCount = sourceFacts.Count;
        var targetCount = targetFacts.Count;
        var available = sourceFacts.Initialized && targetFacts.Empty;
        return new(Descriptor, available, $"{sourceCount} history entries available.", available
            ? "Transfer is a snapshot. Canonical source files and protection keys must remain accessible; they are not copied by this group."
            : "History transfer requires an initialized source and an empty target history partition.", sourceCount, targetCount);
    }

    public Task<DatabaseTransferItemResult> TransferAsync(DatabaseTransferOperation context, CancellationToken cancellationToken = default)
        => operations.RunTransferAsync(context, (transfer, token) => TransferCoreAsync(context, transfer, token), cancellationToken: cancellationToken);

    private async Task<DatabaseTransferItemResult> TransferCoreAsync(DatabaseTransferOperation context,
        DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken) {
        await using var source = await sessions.CreateSourceAsync<ProviderHistoryDbContext>(transfer, static options => new ProviderHistoryDbContext(options), cancellationToken);
        await using var target = await sessions.CreateTargetAsync<ProviderHistoryDbContext>(transfer, static options => new ProviderHistoryDbContext(options), cancellationToken);
        await target.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(724091824013)", cancellationToken);
        if (!await source.Set<HistoryStorageIdentity>().AnyAsync(cancellationToken) ||
            !await IsEmptyTargetAsync(target, cancellationToken) ||
            !context.ReplaceExisting && await target.Set<HistoryStorageIdentity>().AnyAsync(cancellationToken)) {
            throw new InvalidOperationException("History transfer cannot merge or replace retained target history.");
        }
        foreach (var participant in orderedParticipants) {
            await participant.ValidateTargetAsync(transfer, cancellationToken);
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
            count = checked(count + await participant.CopyAsync(transfer, cancellationToken));
        }
        target.Add(await source.Set<HistoryStorageIdentity>().AsNoTracking().SingleAsync(cancellationToken));
        await target.SaveChangesAsync(cancellationToken);
        return new(Descriptor.Key, Descriptor.Label, true, "Copied provider history without changing its storage lineage or recorded expiry.", checked(count + 1));
    }

    private static async Task<bool> IsEmptyTargetAsync(ProviderHistoryDbContext target, CancellationToken cancellationToken)
        => !await target.Set<HistoryEntryRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistorySourceRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryDetailRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryOutboxRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryHostLeaseRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryPolicyAuditRow>().AnyAsync(cancellationToken)
            && !await target.Set<HistoryPolicyRow>().AnyAsync(row => row.Version != 0 || row.UsedDetailBytes != 0, cancellationToken);

    private static async Task RemoveEmptyBootstrapAsync(ProviderHistoryDbContext target, CancellationToken cancellationToken) {
        await target.Set<HistoryStorageIdentity>().ExecuteDeleteAsync(cancellationToken);
        await target.Set<HistoryCheckpointRow>().ExecuteDeleteAsync(cancellationToken);
        await target.Set<HistoryPolicyRow>().ExecuteDeleteAsync(cancellationToken);
        await target.Set<HistoryPartitionRow>().ExecuteDeleteAsync(cancellationToken);
        target.ChangeTracker.Clear();
    }
}
