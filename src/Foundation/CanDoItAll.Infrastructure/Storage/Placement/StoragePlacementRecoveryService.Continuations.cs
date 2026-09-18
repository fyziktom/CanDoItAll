namespace CanDoItAll.Infrastructure.Storage;

public sealed partial class StoragePlacementRecoveryService {
    public async Task<StoragePlacementContinuationPage> ListPendingContinuationsAsync(StoragePlacementRecoveryQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Take is < 1 or > 128 || query.Offset < 0 || query.Offset > int.MaxValue - 129 ||
            query.ProjectId == Guid.Empty || query.StorageId == Guid.Empty) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.InvalidRequest);
        }
        var authorization = await access.AuthorizeAsync(StoragePlacementRecoveryOperation.Read, cancellationToken);
        RequireContext(query.Context);
        var scan = await access.ListContinuationsAsync(authorization, query.ProjectId, query.Take, query.Offset, cancellationToken);
        if (scan.Items.Count > query.Take || scan.Items.Any(item => item.IntentId.Value == Guid.Empty || !Enum.IsDefined(item.Phase)) ||
            scan.Items.Select(item => item.IntentId).Distinct().Count() != scan.Items.Count ||
            scan.NextOffset is { } next && next != query.Offset + query.Take) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Unavailable);
        }
        var phases = scan.Items.ToDictionary(item => item.IntentId.Value, item => item.Phase);
        var rows = await storage.FindCompletedRecoveryAsync(scan.Items.Select(item => item.IntentId).ToArray(),
            query.StorageId, cancellationToken);
        var owners = await access.InspectOwnersAsync(authorization, rows.Select(row => row.Identity).ToArray(), cancellationToken);
        var items = rows.Select(row => new StoragePlacementContinuationItem(
            Project(query.Context, row, Owner(owners, row.Identity), authorization), phases[row.Identity.IntentId.Value])).ToArray();
        await access.EnsureCurrentAsync(authorization, cancellationToken);
        RequireContext(query.Context);
        return new(items, scan.NextOffset);
    }
}
